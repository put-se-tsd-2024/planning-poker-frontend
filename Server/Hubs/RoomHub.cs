﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlanningPoker.Server.Data;
using PlanningPoker.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlanningPoker.Server.Hubs
{
    public class RoomHub : Hub
    {
        private readonly MyDbContext _context;
        private static List<RoomPlay> _roomPlays = new List<RoomPlay>();

        public RoomHub(MyDbContext context)
        {
            _context = context;
        }

        public async Task RegisterPlayerAsync(RegisterPlayerRequest registerPlayerRequest)
        {
            var alreadyExists = _roomPlays.Exists(ga => ga.HubConnectionId == Context.ConnectionId);

            if (alreadyExists)
                return;

            var roomPlay = new RoomPlay
            {
                HubConnectionId = Context.ConnectionId,
                Room = registerPlayerRequest.Room,
                Player = registerPlayerRequest.Player,
                CardPlayed = new Card()
            };

            _roomPlays.Add(roomPlay);

            await Groups.AddToGroupAsync(Context.ConnectionId, registerPlayerRequest.Room.Id);

            await UpdateRoom(roomPlay.Room.Id);
        }

        public async Task PlayCardAsync(PlayCardRequest playCardRequest)
        {
            var roomPlay = _roomPlays.FirstOrDefault(ga => ga.HubConnectionId == Context.ConnectionId);

            if (roomPlay == null)
                return;

            roomPlay.CardPlayed = playCardRequest.CardPlayed;

            await UpdateRoom(roomPlay.Room.Id);
        }

        public async Task ResetRoom(ResetRoomRequest resetRoomRequest)
        {
            var roomsPlaysForThisRoom = _roomPlays.Where(gp => gp.Room.Id == resetRoomRequest.Room.Id).ToList();
            roomsPlaysForThisRoom.ForEach(gp => gp.CardPlayed = new Card());
            roomsPlaysForThisRoom.ForEach(gp => gp.ShowEstimations = false);

            await UpdateRoom(resetRoomRequest.Room.Id);
        }

        public async Task ShowRoomEstimations(ShowEstimationsRequest resetRoomRequest)
        {
            var roomsPlaysForThisRoom = _roomPlays.Where(gp => gp.Room.Id == resetRoomRequest.Room.Id).ToList();
            roomsPlaysForThisRoom.ForEach(gp => gp.ShowEstimations = !gp.ShowEstimations);

            await UpdateRoom(resetRoomRequest.Room.Id);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var roomPlay = _roomPlays.FirstOrDefault(ga => ga.HubConnectionId == Context.ConnectionId);

            if (roomPlay != null)
            {
                _roomPlays.Remove(roomPlay);
                await UpdateRoom(roomPlay.Room.Id);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task UpdateRoom(string roomId)
        {
            var roomsPlaysForThisRoom = _roomPlays.Where(gp => gp.Room.Id == roomId).ToList();

            await Clients.Group(roomId).SendAsync("UpdateRoom", roomsPlaysForThisRoom);
        }

        public async Task CreateUserStoryAsync(UserStory userStory)
        {
            _context.UserStories.Add(userStory);
            await _context.SaveChangesAsync();
            // Notify all clients in the room that a new user story has been created
            await Clients.Group(userStory.RoomId).SendAsync("UserStoryCreated", userStory);
            await UpdateRoom(userStory.RoomId); // Optional if UpdateRoom impacts the state
        }

        public async Task<List<UserStory>> GetUserStoriesAsync(string roomId)
        {
            return await _context.UserStories
                .Include(us => us.Works)
                .Where(us => us.RoomId == roomId)
                .ToListAsync();
        }

        public async Task DeleteUserStoryAsync(int userStoryId)
        {
            var userStory = await _context.UserStories.FindAsync(userStoryId);
            if (userStory != null)
            {
                _context.UserStories.Remove(userStory);
                await _context.SaveChangesAsync();
                // Notify all clients in the room that a user story has been deleted
                await Clients.Group(userStory.RoomId).SendAsync("UserStoryDeleted", userStoryId);
                await UpdateRoom(userStory.RoomId); // Optional if UpdateRoom impacts the state
            }
        }

        public async Task<UserStory> GetUserStoryAsync(string roomId, string userStoryId)
        {
            // Convert the userStoryId to an int
            int id = int.Parse(userStoryId);
            // Fetch the user story from the database
            var userStory = await _context.UserStories
                .Include(us => us.Works)
                .Where(us => us.RoomId == roomId && us.Id == id)
                .FirstOrDefaultAsync();
            return userStory;
        }

        public async Task UpdateUserStoryAsync(UserStory updatedUserStory)
        {
            var userStory = await _context.UserStories.FindAsync(updatedUserStory.Id);
            if (userStory != null)
            {
                userStory.Title = updatedUserStory.Title;
                userStory.Description = updatedUserStory.Description;
                userStory.Works = updatedUserStory.Works;
                userStory.IsCompleted = updatedUserStory.IsCompleted;
                await _context.SaveChangesAsync();
                // Notify all clients in the room that a user story has been updated
                await Clients.Group(userStory.RoomId).SendAsync("UserStoryUpdated", userStory);
                await UpdateRoom(userStory.RoomId); // Optional if UpdateRoom impacts the state
            }
        }

        public async Task SelectUserStory(string userStoryTitle, string workTitle)
        {
            var roomPlay = _roomPlays.FirstOrDefault(ga => ga.HubConnectionId == Context.ConnectionId);

            if (roomPlay == null)
                return;

            await Clients.Group(roomPlay.Room.Id).SendAsync("UserStorySelected", userStoryTitle, workTitle);
        }
    }
}
