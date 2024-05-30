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
            _userColl.InsertOne(userModel);
        }

        public static void ChangePassword(ObjectId userId, string newPass)
        {
            _userColl.FindOneAndUpdate(Builders<UserModel>.Filter.Eq("Id", userId), Builders<UserModel>.Update.Set("Password", newPass));
        }

        public static List<StoryModel> GetRegisteredStories(ObjectId userId, int pageSize, int pageId)
        {
            UserModel user = GetUser(userId);

            return _storyColl.Find(e => user.RegisteredStories.Contains(e.Id)).Skip(pageId * pageSize).Limit(pageSize).ToList();
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
            StoryModel ev = GetStory(storyId);

            if (ev.CreatorId == user.Id)
            {
                status = "creator";
            }
            else if (user.RegisteredStories.Contains(ev.Id))
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

        public static List<StoryModel> GetStories(int pageSize, int pageId)
        {
            return _storyColl.Find(e => true).Skip(pageId * pageSize).Limit(pageSize).ToList();
        }

        public static void AddStory(StoryModel storyModel)
        {
            _storyColl.InsertOne(storyModel);
        }

        public static void UpdateImage(ObjectId storyId, string image)
        {
            var filter = Builders<StoryModel>.Filter.Eq(e => e.Id, storyId);
            var update = Builders<StoryModel>.Update.Set(e => e.Image, image);
            _storyColl.UpdateOne(filter, update);
        }

        public static List<StoryModel> Search(int pageSize, int pageId, string searchText)
        {
            var filter = Builders<StoryModel>.Filter.Text(searchText);
            var projection = Builders<StoryModel>.Projection.MetaTextScore("TextMatchScore");
            var sort = Builders<StoryModel>.Sort.MetaTextScore("TextMatchScore");

            var results = _storyColl.Find(filter)
                                    .Project<StoryModel>(projection)
                                    .Sort(sort)
                                    .Skip(pageId * pageSize)
                                    .Limit(pageSize)
                                    .ToList();

            results.ForEach(story =>
            {
                story.TextMatchScore = null; // Assuming StoryModel has a property for TextMatchScore, otherwise skip this line
            });

            return results;
        }


        public static List<long> countRegistrations(ObjectId storyId)
        {
            StoryModel ev = GetStory(storyId);
            ObjectId creatorId = ev.CreatorId;
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
            StoryModel ev = GetStory(storyId);
            DateTime d1 = ev.DateCreated.AddDays(-ev.DateCreated.Day + 1);
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
            return _reviewColl.Find(r => r.StoryId == storyId).Sort(Builders<ReviewModel>.Sort.Descending(r => r.LastEdit)).Skip(pageId * pageSize).Limit(pageSize).ToList();
        }

        public static ReviewModel GetReview(ObjectId userId, ObjectId storyId)
        {
            return _reviewColl.Find(r => r.UserId == userId && r.StoryId == storyId).FirstOrDefault();
        }

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
            }
            else
            {
                _reviewColl.UpdateOne(r => r.UserId == userId && r.StoryId == storyId,
                                    Builders<ReviewModel>.Update.Set(r => r.Rating, rating)
                                                                .Set(r => r.Opinion, opinion)
                                                                .Set(r => r.LastEdit, lastEdit));
            }
        }

        public static void DeleteReview(ObjectId userId, ObjectId storyId)
        {
            _reviewColl.DeleteOne(r => r.UserId == userId && r.StoryId == storyId);
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
        }
        #endregion

        public static void InitializeConnection(string connectionString, string databaseName)
        {
            _conn = new MongoClient(connectionString);
            _db = _conn.GetDatabase(databaseName);
            _userColl = _db.GetCollection<UserModel>("user");
            _storyColl = _db.GetCollection<StoryModel>("story");
            _reviewColl = _db.GetCollection<ReviewModel>("review");
            _messageColl = _db.GetCollection<MessageModel>("message");

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
    }
}
