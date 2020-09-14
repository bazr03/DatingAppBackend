using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        private readonly DataContext _context;
        public DatingRepository(DataContext context)
        {
            _context = context;
        }
        public void Add<T>(T entity) where T : class
        {
            _context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Remove(entity);
        }

        public async Task<Like> GetLike(int userId, int recipientId)
        {
            return await _context.Likes.FirstOrDefaultAsync(u => u.LikerId == userId && u.LikeeId == recipientId);
        }

        public async Task<Photo> GetMainPhotoForUser(int userId)
        {
            return await _context.Photos.Where(u => u.UserId == userId).FirstOrDefaultAsync(p => p.IsMain);
        }

        public async Task<Photo> GetPhoto(int id)
        {
            var photo = await _context.Photos.FirstOrDefaultAsync(p => p.Id == id);

            return photo;
        }

        public async Task<User> GetUser(int id)
        {
            var user = await _context.Users.Include(p => p.Photos).FirstOrDefaultAsync(u => u.id == id);
            return user;
        }

        public async Task<PageList<User>> GetUsers(UserParams userParams)
        {
            var users = _context.Users.Include(p => p.Photos).OrderByDescending(u => u.LastActive).AsQueryable();

            users = users.Where(u => u.id != userParams.UserId);
            users = users.Where(u => u.Gender == userParams.Gender);

            if (userParams.Likers)
            {
                var userLikers = await GetUserLikes(userParams.UserId, userParams.Likers);
                users = users.Where(u => userLikers.Contains(u.id));
            }

            if (userParams.Likees)
            {
                var userLikees = await GetUserLikes(userParams.UserId, userParams.Likers);
                users = users.Where(u => userLikees.Contains(u.id));
            }

            if (userParams.MaxAge != 18 || userParams.MaxAge != 99)
            {
                var minDoB = DateTime.Today.AddYears(-userParams.MaxAge - 1);
                var maxBoB = DateTime.Today.AddYears(-userParams.MinAge);

                users = users.Where(u => u.DateOfBirth >= minDoB && u.DateOfBirth <= maxBoB);
            }

            if (!string.IsNullOrEmpty(userParams.OrderBy))
            {
                switch (userParams.OrderBy)
                {
                    case "created":
                        users = users.OrderByDescending(u => u.CreatedAt);
                        break;
                    default:
                        users = users.OrderByDescending(u => u.LastActive);
                        break;

                }
            }

            return await PageList<User>.CreateAsync(users, userParams.PageNumber, userParams.PageSize);
        }

        private async Task<IEnumerable<int>> GetUserLikes(int id, bool likerks)
        {
            var user = await _context.Users
                                    .Include(x => x.Likers)
                                    .Include(x => x.Likees)
                                    .FirstOrDefaultAsync(u => u.id == id);

            if(likerks)
            {
                return user.Likers
                            .Where(u => u.LikeeId == id)
                            .Select(i => i.LikerId);
            }
            else 
            {
                return user.Likees
                            .Where(u => u.LikerId == id)
                            .Select(i => i.LikeeId);
            }


        }

        public async Task<bool> SaveAll()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<Message> GetMessage(int id)
        {
            return await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<PageList<Message>> GetMessagesForUser(MessageParams messageParams)
        {
            var messages =  _context.Messages
                                        .Include(u => u.Sender).ThenInclude(p => p.Photos)
                                        .Include(u => u.Recipient).ThenInclude(p => p.Photos)
                                        .AsQueryable();
            
            switch(messageParams.MessageContainer)
            {
                case "Inbox":
                    messages = messages.Where(u => u.RecipientId == messageParams.UserId);
                    break;
                case "Outbox":
                    messages = messages.Where(u => u.SenderId == messageParams.UserId);
                    break;
                default:
                    messages = messages.Where(u => u.RecipientId == messageParams.UserId && u.IsRead == false);
                    break;
            }

            messages = messages.OrderByDescending(d => d.MessageSent);

            return await PageList<Message>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);
        }

        public Task<IEnumerable<Message>> GetMessageThread(int userId, int recipientId)
        {
            throw new NotImplementedException();
        }
    }
}