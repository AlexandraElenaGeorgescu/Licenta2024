using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StoryVerseBackEnd.Models;

namespace StoryVerseBackEnd.Utils
{
    public class MongoUtil
    {
        #region User Util

        public static UserModel GetUser(ObjectId userId)
        {
            return _userColl.Find(u => u.Id == userId).FirstOrDefault();
        }

        public static UserModel GetUser(string email)
        {
            return _userColl.Find(u => u.Email.ToLower() == email.ToLower()).FirstOrDefault();
        }

        public static UserModel GetUser(string email, string password)
        {
            return _userColl.Find(u => u.Email.ToLower() == email.ToLower() && u.Password == password).FirstOrDefault();
        }

        public static void AddUser(UserModel userModel)
        {
            if (userModel.RegisteredStories == null)
            {
                userModel.RegisteredStories = new List<ObjectId>();
            }

            _userColl.InsertOne(userModel);
            AddNotification(userModel.Id, "Welcome to StoryVerse! Your account has been created successfully.");
        }

        public static void DeleteUser(ObjectId userId)
        {
            _userColl.DeleteOne(u => u.Id == userId);

            _reviewColl.DeleteMany(r => r.UserId == userId);

            var userStories = _storyColl.Find(s => s.CreatorId == userId).ToList();
            foreach (var story in userStories)
            {
                _reviewColl.DeleteMany(r => r.StoryId == story.Id);
                _messageColl.DeleteMany(m => m.StoryId == story.Id);
                _storyColl.DeleteOne(s => s.Id == story.Id);
                UpdateReviewCount(story.Id);
            }

            _messageColl.DeleteMany(m => m.UserId == userId);
        }

        public static void UpdateUserAvatar(ObjectId userId, string avatar)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
            var update = Builders<UserModel>.Update.Set(u => u.Avatar, avatar);
            _userColl.UpdateOne(filter, update);
            AddNotification(userId, "Your avatar has been updated.");
        }

        public static void UpdateUserName(ObjectId userId, string name, string surname)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
            var update = Builders<UserModel>.Update.Set(u => u.Name, name).Set(u => u.Surname, surname);
            _userColl.UpdateOne(filter, update);
            AddNotification(userId, "Your name has been updated.");
        }

        public static void ChangePassword(ObjectId userId, string newPass)
        {
            _userColl.FindOneAndUpdate(Builders<UserModel>.Filter.Eq("Id", userId), Builders<UserModel>.Update.Set("Password", newPass));
            AddNotification(userId, "Your password has been changed.");
        }

        public static List<StoryModel> GetRegisteredStories(ObjectId userId, int pageSize, int pageId)
        {
            UserModel user = GetUser(userId);
            var registeredStoryIds = user?.RegisteredStories ?? new List<ObjectId>();

            return _storyColl.Find(e => registeredStoryIds.Contains(e.Id))
                             .Skip(pageId * pageSize)
                             .Limit(pageSize)
                             .ToList();
        }

        public static List<ReviewModel> GetUserReviews(ObjectId userId, int pageSize, int pageId)
        {
            return _reviewColl.Find(r => r.UserId == userId).Skip(pageId * pageSize).Limit(pageSize).ToList();
        }

        public static List<StoryModel> GetCreatedStories(ObjectId userId, int pageSize, int pageId)
        {
            List<StoryModel> resp = _storyColl.Find(e => e.CreatorId == userId).SortByDescending(e => e.DateCreated).Skip(pageId * pageSize).Limit(pageSize).ToList();
            return resp;
        }

        public static String GetRegistrationStatus(ObjectId storyId, ObjectId userId)
        {
            String status = "server problem";
            UserModel user = GetUser(userId);
            StoryModel st = GetStory(storyId);

            if (st.CreatorId == user.Id)
            {
                status = "creator";
            }
            else if (user.RegisteredStories.Contains(st.Id))
            {
                status = "registered";
            }
            else
            {
                status = "unregistered";
            }

            return status;
        }

        public static Boolean RegisterUserToStory(ObjectId storyId, ObjectId userId)
        {
            String status = GetRegistrationStatus(storyId, userId);
            if (status == "unregistered")
            {
                _userColl.FindOneAndUpdate(Builders<UserModel>.Filter.Eq("Id", userId), Builders<UserModel>.Update.Push("RegisteredStories", storyId));
                AddNotification(userId, "You have registered for a new story.");
                UpdateStorySubscribersCount(storyId);
                return true;
            }
            return false;
        }

        public static Boolean UnregisterUserFromStory(ObjectId storyId, ObjectId userId)
        {
            String status = GetRegistrationStatus(storyId, userId);
            if (status == "registered")
            {
                _userColl.FindOneAndUpdate(Builders<UserModel>.Filter.Eq("Id", userId), Builders<UserModel>.Update.Pull("RegisteredStories", storyId));
                AddNotification(userId, "You have unregistered from a story.");
                UpdateStorySubscribersCount(storyId);
                return true;
            }
            return false;
        }

        #endregion

        #region Story Util

        public static StoryModel GetStory(ObjectId storyId)
        {
            return _storyColl.Find(e => e.Id == storyId).FirstOrDefault();
        }

        public static List<StoryModel> Search(int pageSize, int pageId, string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                throw new ArgumentException("Search text cannot be null or empty.");
            }
            if (pageSize <= 0)
            {
                throw new ArgumentException("Page size must be greater than zero.");
            }
            if (pageId < 0)
            {
                throw new ArgumentException("Page ID cannot be negative.");
            }

            try
            {
                var textFilter = Builders<StoryModel>.Filter.Text(searchText);
                
                var projection = Builders<StoryModel>.Projection.MetaTextScore("TextMatchScore");
                var sort = Builders<StoryModel>.Sort.MetaTextScore("TextMatchScore");

                var results = _storyColl.Find(textFilter)
                                        .Project<StoryModel>(projection)
                                        .Sort(sort)
                                        .Skip(pageId * pageSize)
                                        .Limit(pageSize)
                                        .Project(story => new StoryModel
                                        {
                                            Id = story.Id,
                                            Name = story.Name,
                                            DateCreated = story.DateCreated,
                                            Genre = story.Genre,
                                            Description = story.Description,
                                            ActualStory = story.ActualStory,
                                            Image = story.Image,
                                            Author = GetUser(story.CreatorId).Name + " " + GetUser(story.CreatorId).Surname,
                                        })
                                        .ToList();

                results.ForEach(story =>
                {
                    story.TextMatchScore = null;
                });

                return results;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while searching stories. Please try again later.");
            }
        }

        public static List<string> GetAllGenres()
        {
            var collection = _storyColl;
            var genres = collection.Distinct<string>("Genre", new BsonDocument()).ToList();
            return genres;
        }

        public static List<StoryModel> GetStories(int pageSize, int pageId, string genre = "")
        {
            if (pageSize <= 0)
            {
                throw new ArgumentException("Page size must be greater than zero.");
            }
            if (pageId < 0)
            {
                throw new ArgumentException("Page ID cannot be negative.");
            }

            try
            {
                var filter = string.IsNullOrEmpty(genre) ? Builders<StoryModel>.Filter.Empty : Builders<StoryModel>.Filter.Eq(s => s.Genre, genre);

                return _storyColl.Find(filter)
                                 .Skip(pageId * pageSize)
                                 .Limit(pageSize)
                                 .Project(story => new StoryModel
                                 {
                                     Id = story.Id,
                                     Name = story.Name,
                                     DateCreated = story.DateCreated,
                                     Genre = story.Genre,
                                     Description = story.Description,
                                     ActualStory = story.ActualStory,
                                     Image = story.Image,
                                     Author = GetUser(story.CreatorId).Name + " " + GetUser(story.CreatorId).Surname,
                                     AuthorAvatarUrl = GetUser(story.CreatorId).Avatar,
                                     AverageRating = story.AverageRating,
                                     BookmarksCount = story.BookmarksCount,
                                     ReviewCount = story.ReviewCount,
                                 })
                                 .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while fetching stories. Please try again later.");
            }
        }

        public static void AddStory(StoryModel storyModel)
        {
            _storyColl.InsertOne(storyModel);
        }

        public static void UpdateStory(StoryModel storyModel)
        {
            var filter = Builders<StoryModel>.Filter.Eq(e => e.Id, storyModel.Id);
            var update = Builders<StoryModel>.Update
                            .Set(e => e.Name, storyModel.Name)
                            .Set(e => e.Description, storyModel.Description)
                            .Set(e => e.Genre, storyModel.Genre)
                            .Set(e => e.ActualStory, storyModel.ActualStory)
                            .Set(e => e.Image, storyModel.Image)
                            .Set(e => e.Author, GetUser(storyModel.CreatorId).Name + " " + GetUser(storyModel.CreatorId).Surname)
                            .Set(e => e.AuthorAvatarUrl, GetUser(storyModel.CreatorId).Avatar);

            UpdateStorySubscribersCount(storyModel.Id);
            UpdateStoryRatings(storyModel.Id);
            UpdateStoryBookmarksCount(storyModel.Id);
            _storyColl.UpdateOne(filter, update);
            NotifyUsersOnStoryUpdate(storyModel.Id);
        }

        public static void UpdateImage(ObjectId storyId, string image)
        {
            var filter = Builders<StoryModel>.Filter.Eq(e => e.Id, storyId);
            var update = Builders<StoryModel>.Update.Set(e => e.Image, image);
            _storyColl.UpdateOne(filter, update);
            NotifyUsersOnStoryUpdate(storyId);
        }

        public static List<long> countRegistrations(ObjectId storyId)
        {
            StoryModel st = GetStory(storyId);
            ObjectId creatorId = st.CreatorId;
            List<ObjectId> otherStories = _storyColl.Find(e => e.CreatorId == creatorId && e.Id != storyId).Project(e => e.Id).ToList();
            List<UserModel> allUsers = _userColl.Find(u => true).ToList();

            long subscribedToThisStory = allUsers.Where(u => u.RegisteredStories != null && u.RegisteredStories.Contains(storyId) && !u.RegisteredStories.Intersect(otherStories).Any()).Count();
            long subscribedToOthers = allUsers.Where(u => u.RegisteredStories != null && !u.RegisteredStories.Contains(storyId) && u.RegisteredStories.Intersect(otherStories).Any()).Count();
            long subscribedBoth = allUsers.Where(u => u.RegisteredStories != null && u.RegisteredStories.Contains(storyId) && u.RegisteredStories.Intersect(otherStories).Any()).Count();
            long total = allUsers.Where(u => u.RegisteredStories != null && (u.RegisteredStories.Contains(storyId) || u.RegisteredStories.Intersect(otherStories).Any())).Count();

            return new List<long> { subscribedToThisStory, subscribedToOthers, subscribedBoth };
        }

        public static Tuple<List<long>, List<string>> countMsgs(ObjectId storyId)
        {
            List<long> resp = new List<long>();
            List<string> labels = new List<string>();
            StoryModel st = GetStory(storyId);
            DateTime d1 = st.DateCreated.AddDays(-st.DateCreated.Day + 1);
            DateTime d2 = DateTime.Now;
            List<MessageModel> msgs = _messageColl.Find(m => m.StoryId == storyId).ToList();

            for (DateTime d = d1; d.Date < d2.Date; d = d.AddMonths(1))
            {
                long x = msgs.Where(m => m.DateSent.Year == d.Year && m.DateSent.Month == d.Month).Count();
                resp.Add(x);
            }

            labels.Add(String.Format("{0:dd-MM-yy}", d1));
            labels.Add(String.Format("{0:dd-MM-yy}", d2));
            labels.Add(msgs.Count.ToString());

            return new Tuple<List<long>, List<string>>(resp, labels);
        }

        public static Tuple<long, float> getReviewsStats(ObjectId storyId)
        {
            var reviews = _reviewColl.AsQueryable().Where(r => r.StoryId == storyId).ToList();
            long reviewCount = reviews.Count;
            float averageRating = reviewCount > 0 ? reviews.Average(r => r.Rating) : 0;

            return new Tuple<long, float>(reviewCount, averageRating);
        }

        #endregion

        #region Review Util

        public static List<ReviewModel> GetReviews(ObjectId storyId, int pageSize, int pageId)
        {
            if (storyId == ObjectId.Empty)
            {
                throw new ArgumentException("Invalid story ID.");
            }
            if (pageSize <= 0)
            {
                throw new ArgumentException("Page size must be greater than zero.");
            }
            if (pageId < 0)
            {
                throw new ArgumentException("Page ID cannot be negative.");
            }

            try
            {
                return _reviewColl.Find(r => r.StoryId == storyId)
                                  .Sort(Builders<ReviewModel>.Sort.Descending(r => r.LastEdit))
                                  .Skip(pageId * pageSize)
                                  .Limit(pageSize)
                                  .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while fetching reviews. Please try again later.");
            }
        }

        public static ReviewModel GetReview(ObjectId userId, ObjectId storyId)
        {
            return _reviewColl.Find(r => r.UserId == userId && r.StoryId == storyId).FirstOrDefault();        }

        public static void EditReview(ObjectId userId, ObjectId storyId, int rating, String opinion, DateTime lastEdit)
        {
            if (_reviewColl.Find(r => r.UserId == userId && r.StoryId == storyId).CountDocuments() == 0)
            {
                ReviewModel reviewModel = new ReviewModel
                {
                    Id = new ObjectId(),
                    Rating = rating,
                    Opinion = opinion,
                    LastEdit = lastEdit,
                    UserId = userId,
                    StoryId = storyId
                };

                _reviewColl.InsertOne(reviewModel);
                UpdateReviewCount(storyId);
                NotifyAuthorOnNewReview(storyId, userId);
            }
            else
            {
                _reviewColl.UpdateOne(r => r.UserId == userId && r.StoryId == storyId,
                                    Builders<ReviewModel>.Update.Set(r => r.Rating, rating)
                                                                .Set(r => r.Opinion, opinion)
                                                                .Set(r => r.LastEdit, lastEdit));
                NotifyAuthorOnReviewUpdate(storyId, userId);
                UpdateStoryRatings(storyId);
            }
        }

        public static void DeleteReview(ObjectId userId, ObjectId storyId)
        {
            _reviewColl.DeleteOne(r => r.UserId == userId && r.StoryId == storyId);
            UpdateReviewCount(storyId);
            NotifyAuthorOnReviewDeletion(storyId, userId);
        }

        #endregion

        #region ChatRoom Util

        public static List<MessageModel> GetMessages(ObjectId storyId)
        {
            return _messageColl.Find(m => m.StoryId == storyId).SortBy(m => m.DateSent).ToList();
        }

        public static void SaveMessage(MessageModel messageModel)
        {
            _messageColl.InsertOne(messageModel);
            NotifyUsersOnNewMessage(messageModel.StoryId, messageModel.UserId, messageModel.Message);
            NotifyAuthorOnNewMessage(messageModel.StoryId);
        }

        #endregion

        public static void DeleteStory(ObjectId storyId)
        {
            var story = GetStory(storyId);
            if (story != null)
            {
                _storyColl.DeleteOne(e => e.Id == storyId);
                _reviewColl.DeleteMany(r => r.StoryId == storyId);
                _messageColl.DeleteMany(m => m.StoryId == storyId);
                _userColl.UpdateMany(u => u.RegisteredStories.Contains(storyId),
                                       Builders<UserModel>.Update.Pull(u => u.RegisteredStories, storyId));
                _userColl.UpdateMany(u => u.BookmarkedStories.Contains(storyId), Builders<UserModel>.Update.Pull(u => u.BookmarkedStories, storyId));
                NotifyUsersOnStoryDeletion(storyId);
            }
        }

        public static void BookmarkStory(ObjectId storyId, ObjectId userId)
        {
            _userColl.FindOneAndUpdate(
                Builders<UserModel>.Filter.Eq(u => u.Id, userId),
                Builders<UserModel>.Update.AddToSet(u => u.BookmarkedStories, storyId)
            );
            AddNotification(userId, "You have bookmarked a story.");
            UpdateStoryBookmarksCount(storyId);
        }

        public static List<StoryModel> GetBookmarkedStories(ObjectId userId)
        {
            var user = GetUser(userId);
            if (user == null || user.BookmarkedStories == null)
            {
                return new List<StoryModel>();
            }

            return _storyColl.Find(s => user.BookmarkedStories.Contains(s.Id)).ToList();
        }

        public static List<StoryModel> GetRecommendations(ObjectId userId)
        {
            var user = GetUser(userId);

            if (user == null)
            {
                return new List<StoryModel>();
            }

            var reviewedGenres = _reviewColl
                .Find(r => r.UserId == userId)
                .ToList()
                .Select(r => GetStory(r.StoryId)?.Genre) 
                .Where(genre => genre != null)
                .Distinct()
                .ToList();

            var bookmarkedGenres = (user.BookmarkedStories ?? new List<ObjectId>())
                .Select(storyId => GetStory(storyId)?.Genre)
                .Where(genre => genre != null)
                .Distinct()
                .ToList();

            var preferredGenres = reviewedGenres
                .Union(bookmarkedGenres)
                .Distinct()
                .ToList();

            var recommendedStories = _storyColl
                .Find(s => preferredGenres.Contains(s.Genre) && !user.RegisteredStories.Contains(s.Id))
                .Limit(5)
                .ToList();

            return recommendedStories;
        }

        public static void AddNotification(ObjectId userId, string message)
        {
            var notification = new NotificationModel
            {
                UserId = userId,
                Message = message,
                Date = DateTime.Now,
                Read = false
            };
            _notificationColl.InsertOne(notification);
        }

        public static List<NotificationModel> GetNotifications(ObjectId userId)
        {
            return _notificationColl.Find(n => n.UserId == userId).SortByDescending(n => n.Date).ToList();
        }

        public static void MarkNotificationAsRead(ObjectId notificationId, ObjectId userId)
        {
            var filter = Builders<NotificationModel>.Filter.Eq(n => n.Id, notificationId) & Builders<NotificationModel>.Filter.Eq(n => n.UserId, userId);
            var update = Builders<NotificationModel>.Update.Set(n => n.Read, true);
            _notificationColl.UpdateOne(filter, update);
        }

        public static void NotifyAuthorOnNewReview(ObjectId storyId, ObjectId reviewerId)
        {
            var story = GetStory(storyId);
            var reviewer = GetUser(reviewerId);

            AddNotification(story.CreatorId, $"{reviewer.Name} {reviewer.Surname} reviewed your story: {story.Name}");
        }

        public static void NotifyAuthorOnReviewUpdate(ObjectId storyId, ObjectId reviewerId)
        {
            var story = GetStory(storyId);
            var reviewer = GetUser(reviewerId);

            AddNotification(story.CreatorId, $"{reviewer.Name} {reviewer.Surname} updated their review on your story: {story.Name}");
        }

        public static void NotifyAuthorOnReviewDeletion(ObjectId storyId, ObjectId reviewerId)
        {
            var story = GetStory(storyId);
            var reviewer = GetUser(reviewerId);

            AddNotification(story.CreatorId, $"{reviewer.Name} {reviewer.Surname} deleted their review on your story: {story.Name}");
        }
        public static void NotifyAuthorOnNewMessage(ObjectId storyId)
        {
            var story = GetStory(storyId);

            AddNotification(story.CreatorId, $"A new message was sent on your story: {story.Name}");
        }

        public static void NotifyUsersOnNewMessage(ObjectId storyId, ObjectId senderId, string message)
        {
            var story = GetStory(storyId);
            var users = _userColl.Find(u => u.BookmarkedStories.Contains(storyId) || u.RegisteredStories.Contains(storyId)).ToList();

            foreach (var user in users)
            {
                AddNotification(user.Id, $"New message in story {story.Name}: {message}");
            }
        }
        public static void NotifyUsersOnStoryUpdate(ObjectId storyId)
        {
            var story = GetStory(storyId);
            var users = _userColl.Find(u => u.BookmarkedStories.Contains(storyId) || u.RegisteredStories.Contains(storyId)).ToList();

            foreach (var user in users)
            {
                AddNotification(user.Id, $"Story updated: {story.Name}");
            }
        }

        public static void NotifyUsersOnStoryDeletion(ObjectId storyId)
        {
            var story = GetStory(storyId);
            var users = _userColl.Find(u => u.BookmarkedStories.Contains(storyId) || u.RegisteredStories.Contains(storyId)).ToList();

            foreach (var user in users)
            {
                AddNotification(user.Id, $"A story you followed or were registered to has been deleted");
            }
        }

        public static List<StoryModel> GetHighestRatedStories()
        {
            var highestRated = _storyColl.Aggregate()
                .Match(Builders<StoryModel>.Filter.Empty)
                .Sort(Builders<StoryModel>.Sort.Descending("averageRating"))
                .ToList();

            double highestRating = highestRated.FirstOrDefault()?.AverageRating ?? 0;

            var highestRatedWithMostReviews = highestRated
                .Where(story => story.AverageRating == highestRating)
                .OrderByDescending(story => story.ReviewCount)
                .ToList();

            int highestReviewCount = highestRatedWithMostReviews.FirstOrDefault()?.ReviewCount ?? 0;

            var topHighestRatedStories = highestRatedWithMostReviews
                .Where(story => story.ReviewCount == highestReviewCount)
                .ToList();

            return topHighestRatedStories;
        }

        public static List<StoryModel> GetMostSubscribedStories()
        {
            var mostSubscribed = _storyColl.Aggregate()
                .Match(Builders<StoryModel>.Filter.Empty)
                .Sort(Builders<StoryModel>.Sort.Descending("subscribersCount"))
                .ToList();

            int highestSubscribersCount = mostSubscribed.FirstOrDefault()?.SubscribersCount ?? 0;

            var topMostSubscribedStories = mostSubscribed
                .Where(story => story.SubscribersCount == highestSubscribersCount)
                .ToList();

            return topMostSubscribedStories;
        }

        public static List<StoryModel> GetMostBookmarkedStories()
        {
            var mostBookmarked = _storyColl.Aggregate()
                .Match(Builders<StoryModel>.Filter.Empty)
                .Sort(Builders<StoryModel>.Sort.Descending("bookmarksCount"))
                .ToList();

            int highestBookmarksCount = mostBookmarked.FirstOrDefault()?.BookmarksCount ?? 0;

            var topMostBookmarkedStories = mostBookmarked
                .Where(story => story.BookmarksCount == highestBookmarksCount)
                .ToList();

            return topMostBookmarkedStories;
        }


        public static List<StoryModel> PopulateAuthorInfo(List<StoryModel> stories)
        {
            var creatorIds = stories.Select(story => story.CreatorId).Distinct().ToList();
            var users = _userColl.Find(user => creatorIds.Contains(user.Id)).ToList();
            var userDictionary = users.ToDictionary(user => user.Id, user => user);

            foreach (var story in stories)
            {
                if (userDictionary.TryGetValue(story.CreatorId, out var user))
                {
                    story.Author = $"{user.Name} {user.Surname}";
                    story.AuthorAvatarUrl = user.Avatar;
                }
            }

            return stories;
        }
        public static void UpdateReviewCount(ObjectId storyId)
        {
            var reviewCount = _reviewColl.CountDocuments(r => r.StoryId == storyId);
            _storyColl.UpdateOne(
                Builders<StoryModel>.Filter.Eq(s => s.Id, storyId),
                Builders<StoryModel>.Update.Set(s => s.ReviewCount, (int)reviewCount)
            );
        }

        public static void UpdateStoryRatings(ObjectId storyId)
        {
            var reviews = _reviewColl.Find(r => r.StoryId == storyId).ToList();
            var averageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
            _storyColl.UpdateOne(
                Builders<StoryModel>.Filter.Eq(s => s.Id, storyId),
                Builders<StoryModel>.Update.Set(s => s.AverageRating, averageRating)
            );
        }

        public static void UpdateStorySubscribersCount(ObjectId storyId)
        {
            var count = _userColl.CountDocuments(u => u.RegisteredStories.Contains(storyId));
            _storyColl.UpdateOne(
                Builders<StoryModel>.Filter.Eq(s => s.Id, storyId),
                Builders<StoryModel>.Update.Set(s => s.SubscribersCount, (int)count)
            );
        }

        public static void UpdateStoryBookmarksCount(ObjectId storyId)
        {
            var count = _userColl.CountDocuments(u => u.BookmarkedStories.Contains(storyId));
            _storyColl.UpdateOne(
                Builders<StoryModel>.Filter.Eq(s => s.Id, storyId),
                Builders<StoryModel>.Update.Set(s => s.BookmarksCount, (int)count)
            );
        }

        public static void InitializeConnection(string connectionString, string databaseName)
        {
            _conn = new MongoClient(connectionString);
            _db = _conn.GetDatabase(databaseName);
            _userColl = _db.GetCollection<UserModel>("user");
            _storyColl = _db.GetCollection<StoryModel>("story");
            _reviewColl = _db.GetCollection<ReviewModel>("review");
            _messageColl = _db.GetCollection<MessageModel>("message");
            _notificationColl = _db.GetCollection<NotificationModel>("notification");

            var indexKeysDefinition = Builders<StoryModel>.IndexKeys
                .Text(story => story.Name)
                .Text(story => story.Description)
                .Text(story => story.ActualStory);

            var indexModel = new CreateIndexModel<StoryModel>(indexKeysDefinition);
            _storyColl.Indexes.CreateOne(indexModel);
        }

        private static MongoClient _conn;
        private static IMongoDatabase _db;
        private static IMongoCollection<UserModel> _userColl;
        private static IMongoCollection<StoryModel> _storyColl;
        private static IMongoCollection<ReviewModel> _reviewColl;
        private static IMongoCollection<MessageModel> _messageColl;
        private static IMongoCollection<NotificationModel> _notificationColl;
    }
}
