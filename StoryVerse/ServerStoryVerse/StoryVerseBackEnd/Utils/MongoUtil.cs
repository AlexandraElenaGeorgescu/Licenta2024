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

        public static List<StoryModel> GetRegisteredstorys(ObjectId userId, int pageSize, int pageId)
        {
            UserModel user = GetUser(userId);

            return _storyColl.Find(e => user.Registeredstorys.Contains(e.Id)).Skip(pageId * pageSize).Limit(pageSize).ToList();
        }
        
        public static List<ReviewModel> GetUserReviews(ObjectId userId, int pageSize, int pageId)
        {
            return _reviewColl.Find(r => r.UserId == userId).Skip(pageId * pageSize).Limit(pageSize).ToList();
        }

        public static List<StoryModel> GetCreatedstorys(ObjectId userId, int pageSize, int pageId)
        {
            List<StoryModel> resp = _storyColl.Find(e => e.CreatorId == userId).SortByDescending(e => e.DateCreated).Skip(pageId * pageSize).Limit(pageSize).ToList();
            return resp;
        }
        
        public static String GetRegistrationStatus(ObjectId storyId, ObjectId userId)
        {
            String status = "server problem";
            UserModel user = GetUser(userId);
            StoryModel ev = Getstory(storyId);

            if(ev.CreatorId == user.Id)
            {
                status = "creator";
            }
            else if(user.Registeredstorys.Contains(ev.Id))
            {
                status = "registered";
            }
            else
            {
                status = "unregistered";
            }

            return status;
        }

        public static Boolean RegisterUserTostory(ObjectId storyId, ObjectId userId)
        {
            String status = GetRegistrationStatus(storyId, userId);
            if (status == "unregistered")
            {
                _userColl.FindOneAndUpdate(Builders<UserModel>.Filter.Eq("Id", userId), Builders<UserModel>.Update.Push("Registeredstorys", storyId));
                return true;
            }
            return false;
        }
        
        public static Boolean UnregisterUserFromstory(ObjectId storyId, ObjectId userId)
        {
            String status = GetRegistrationStatus(storyId, userId);
            if (status == "registered")
            {
                _userColl.FindOneAndUpdate(Builders<UserModel>.Filter.Eq("Id", userId), Builders<UserModel>.Update.Pull("Registeredstorys", storyId));
                return true;
            }
            return false;
        }

        #endregion

        #region Story Util

        public static StoryModel Getstory(ObjectId storyId)
        {
            return _storyColl.Find(e => e.Id == storyId).FirstOrDefault();
        }
        
        public static List<StoryModel> Getstorys(int pageSize, int pageId)
        {
            return _storyColl.Find(e => true).Skip(pageId * pageSize).Limit(pageSize).ToList();
        }

        public static void Addstory(StoryModel storyModel)
        {
            _storyColl.InsertOne(storyModel);
        }

        public static void UpdateImage(ObjectId storyId, string image)
        {
            var filter = Builders<StoryModel>.Filter.Eq(e => e.Id, storyId);
            var update = Builders<StoryModel>.Update.Set(e => e.Image, image);
            _storyColl.UpdateOne(filter, update);
        }

        public static List<StoryModel> Search(int pageSize, int pageId, String searchText)
        {
            FilterDefinition<StoryModel> filter = Builders<StoryModel>.Filter.Text(searchText);
            ProjectionDefinition<StoryModel> projection = Builders<StoryModel>.Projection.MetaTextScore("TextMatchScore");
            SortDefinition<StoryModel> sort = Builders<StoryModel>.Sort.MetaTextScore("TextMatchScore");

            return _storyColl.Find(filter).Project(projection).Sort(sort).Skip(pageId * pageSize).Limit(pageSize)
                .ToList().ConvertAll(new Converter<BsonDocument, StoryModel>(b => {
                    b.Remove("TextMatchScore");
                    return new StoryModel
                    {
                        Id = b["_id"].AsObjectId,
                        Name = b["name"].AsString,
                        StartDate = b["startDate"].ToUniversalTime(),
                        EndDate = b["endDate"].ToUniversalTime(),
                        Location = b["location"].AsString,
                        Description = b["description"].AsString,
                        Url = b["url"].AsString,
                        Image = b["image"].AsString,
                        CreatorId = b["creatorId"].AsObjectId
                    };
                }));
        }
        
        public static List<long> countRegistrations(ObjectId storyId)
        {
            StoryModel ev = Getstory(storyId);
            ObjectId creatorId = ev.CreatorId;
            List<ObjectId> otherstorys = _storyColl.Find(e => e.CreatorId == creatorId && e.Id != storyId).Project(e => e.Id).ToList();
            List<UserModel> allUsers = _userColl.Find(u => true).ToList();
            
            long subscribedToThisstory = allUsers.Where(u => u.Registeredstorys != null && u.Registeredstorys.Contains(storyId) && !u.Registeredstorys.Intersect(otherstorys).Any()).Count();
            long subscribedToOthers = allUsers.Where(u => u.Registeredstorys != null && !u.Registeredstorys.Contains(storyId) && u.Registeredstorys.Intersect(otherstorys).Any()).Count();
            long subscribedBoth = allUsers.Where(u => u.Registeredstorys != null && u.Registeredstorys.Contains(storyId) && u.Registeredstorys.Intersect(otherstorys).Any()).Count();
            long total = allUsers.Where(u => u.Registeredstorys != null && (u.Registeredstorys.Contains(storyId) || u.Registeredstorys.Intersect(otherstorys).Any())).Count();

            return new List<long> { subscribedToThisstory, subscribedToOthers, subscribedBoth };
        }

        public static Tuple<List<long>, List<string>> countMsgs(ObjectId storyId)
        {
            List<long> resp = new List<long>();
            List<string> labels = new List<string>();
            StoryModel ev = Getstory(storyId);
            DateTime d1 = ev.DateCreated.AddDays(-ev.DateCreated.Day + 1);
            DateTime d2 = DateTime.Now;
            List<MessageModel> msgs = _messageColl.Find(m => m.storyId == storyId).ToList();

            for(DateTime d = d1; d.Date < d2.Date; d = d.AddMonths(1))
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
            return new Tuple<long, float>
            (
                _reviewColl.CountDocuments(r => r.storyId == storyId),
                _reviewColl.AsQueryable().Where(r => r.storyId == storyId).ToList().Average(r => r.Rating)
            );
        }

        #endregion

        #region Review Util

        public static List<ReviewModel> GetReviews(ObjectId storyId, int pageSize, int pageId)
        {
            return _reviewColl.Find(r => r.storyId == storyId).Sort(Builders<ReviewModel>.Sort.Descending(r => r.LastEdit)).Skip(pageId * pageSize).Limit(pageSize).ToList();
        }

        public static ReviewModel GetReview(ObjectId userId, ObjectId storyId)
        {
            return _reviewColl.Find(r => r.UserId == userId && r.storyId == storyId).FirstOrDefault();
        }

        public static void EditReview(ObjectId userId, ObjectId storyId, int rating, String opinion, DateTime lastEdit)
        {
            if(_reviewColl.Find(r => r.UserId == userId && r.storyId == storyId).CountDocuments() == 0)
            {
                ReviewModel reviewModel = new ReviewModel
                {
                    Id = new ObjectId(),
                    Rating = rating,
                    Opinion = opinion,
                    LastEdit = lastEdit,
                    UserId = userId,
                    storyId = storyId
                };

                _reviewColl.InsertOne(reviewModel);
            }
            else
            {
                _reviewColl.UpdateOne(r => r.UserId == userId && r.storyId == storyId,
                                    Builders<ReviewModel>.Update.Set(r => r.Rating, rating)
                                                                .Set(r => r.Opinion, opinion)
                                                                .Set(r => r.LastEdit, lastEdit));
            }
        }

        public static void DeleteReview(ObjectId userId, ObjectId storyId)
        {
            _reviewColl.DeleteOne(r => r.UserId == userId && r.storyId == storyId);
        }

        public static List<StoryModel> getRecommendations()
        {
            long totalUsers = _userColl.CountDocuments(u => true);
            long totalstorys = _storyColl.CountDocuments(e => true);

            using (StreamWriter file = new StreamWriter("AllReviewsMatrix.txt"))
            {
               file.WriteLine(totalUsers + " " + totalstorys);

                for (int i = 0; i < totalUsers; i++)
                {
                    UserModel user = _userColl.Find(u => true).Skip(i).Limit(1).FirstOrDefault();
                    for (int j = 0; j < totalstorys; j++)
                    {
                        StoryModel evnt = _storyColl.Find(u => true).Skip(j).Limit(1).FirstOrDefault();
                        ReviewModel review = GetReview(user.Id, evnt.Id);
                        if (review != null)
                        {
                            file.Write(review.Rating + " ");
                        }
                        else
                        {
                            file.Write("0 ");
                        }
                    }
                    file.WriteLine();
                }
            }

            return null;
        }

        #endregion

        #region ChatRoom Util
        public static List<MessageModel> GetMessages(ObjectId storyId)
        {
            return _messageColl.Find(m => m.storyId == storyId).SortBy(m => m.DateSent).ToList();
        }

        public static void SaveMessage(MessageModel messageModel)
        {
            _messageColl.InsertOne(messageModel);
        }
        #endregion

        public static void InitializeConnection(String connectionString, String databaseName)
        {
            _conn = new MongoClient(connectionString);
            _db = _conn.GetDatabase(databaseName);
            _userColl = _db.GetCollection<UserModel>("user");
            _storyColl = _db.GetCollection<StoryModel>("story");
            _reviewColl = _db.GetCollection<ReviewModel>("review");
            _messageColl = _db.GetCollection<MessageModel>("message");
        }

        private static MongoClient _conn;
        private static IMongoDatabase _db;
        private static IMongoCollection<UserModel> _userColl;
        private static IMongoCollection<StoryModel> _storyColl;
        private static IMongoCollection<ReviewModel> _reviewColl;
        private static IMongoCollection<MessageModel> _messageColl;
    }
}
