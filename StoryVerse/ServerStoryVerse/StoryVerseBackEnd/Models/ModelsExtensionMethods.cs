using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace StoryVerseBackEnd.Models
{
    public static class ModelsExtensionMethods
    {
        public static UserModel getUserModel(this UserApiModel userApiModel, String Id)
        {
            UserModel userModel = new UserModel
            {
                Id = new ObjectId(Id),
                Email = userApiModel.Email.ToLower(),
                Password = userApiModel.Password,
                Birthday = DateTime.ParseExact(userApiModel.Birthday, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Name = userApiModel.Name,
                Surname = userApiModel.Surname,
                RegisteredStories = new List<ObjectId>()
            };

            return userModel;
        }

        public static UserApiModel getUserApiModel(this UserModel userModel)
        {
            UserApiModel userApiModel = new UserApiModel
            {
                Email = userModel.Email.ToLower(),
                Password = userModel.Password,
                Birthday = String.Format("{0:yyyy-MM-dd HH:mm}", userModel.Birthday),
                Name = userModel.Name,
                Surname = userModel.Surname

            };

            return userApiModel;
        }

        public static StoryModel getStoryModel(this StoryApiModel storyApiModel, String CreatorId, DateTime dateCreated)
        {
            StoryModel storyModel = new StoryModel
            {
                Id = new ObjectId(),
                Name = storyApiModel.Name,
                DateCreated = DateTime.Now,
                Genre = storyApiModel.Genre,
                Description = storyApiModel.Description,
                ActualStory = storyApiModel.ActualStory,
                Image = storyApiModel.Image,
                CreatorId = new ObjectId(CreatorId)
            };

            return storyModel;
        }

        public static StoryApiModel getstoryApiModel(this StoryModel storyModel)
        {
            StoryApiModel storyApiModel = new StoryApiModel
            {
                Id = storyModel.Id.ToString(),
                Name = storyModel.Name,
                DateCreated = String.Format("{0:yyyy-MM-dd HH:mm}", storyModel.DateCreated),
                Genre = storyModel.Genre,
                Description = storyModel.Description,
                ActualStory = storyModel.ActualStory,
                Image = storyModel.Image
            };

            return storyApiModel;
        }

        public static ReviewModel getReviewModel(this ReviewApiModel reviewApiModel, String Id, String UserId, String storyId)
        {
            ReviewModel reviewModel = new ReviewModel
            {
                Id = new ObjectId(Id),
                Rating = reviewApiModel.Rating,
                Opinion = reviewApiModel.Opinion,
                LastEdit = DateTime.ParseExact(reviewApiModel.LastEdit, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                UserId = new ObjectId(UserId),
                StoryId = new ObjectId(storyId)
            };

            return reviewModel;
        }

        public static ReviewApiModel getReviewApiModel(this ReviewModel reviewModel, String userName)
        {
            ReviewApiModel reviewApiModel = new ReviewApiModel
            {
                Rating = (int)reviewModel.Rating,
                Opinion = reviewModel.Opinion,
                LastEdit = String.Format("{0:yyyy-MM-dd HH:mm}", reviewModel.LastEdit),
                UserName = userName
            };

            return reviewApiModel;
        }

        public static MessageApiModel getMessageApiModel(this MessageModel messageModel, string userName = "")
        {
            MessageApiModel messagesApiModel = new MessageApiModel
            {
                Message = messageModel.Message,
                DateSent = String.Format("{0:yyyy-MM-dd HH:mm}", messageModel.DateSent),
                UserName = userName
            };

            return messagesApiModel;
        }

        public const String zeroId = "000000000000000000000000";
    }
}
