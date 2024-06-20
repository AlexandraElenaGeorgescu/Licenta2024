using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using StoryVerseBackEnd.Models;
using StoryVerseBackEnd.Utils;

namespace StoryVerseBackEnd.Controllers
{
    [Route("api/review")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        [HttpGet("browse/{storyId}/{pageSize}/{pageId}")]
        public IActionResult GetReview([FromRoute] string storyId, [FromRoute] int pageSize, [FromRoute] int pageId)
        {
            List<ReviewModel> reviews = MongoUtil.GetReviews(new ObjectId(storyId), pageSize, pageId);
            List<ReviewApiModel> apiReviews = reviews.ConvertAll(new Converter<ReviewModel, ReviewApiModel>(r => {
                UserModel user = MongoUtil.GetUser(r.UserId);
                ReviewApiModel apiR = r.getReviewApiModel(user.Name, user.Avatar);
                return apiR;
            }));

            return Ok(apiReviews);
        }

        [HttpGet("get/{storyId}"), Authorize]
        public IActionResult GetReview([FromRoute] string storyId, [FromHeader(Name = "Authorization")] string userToken)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(userToken));
            ReviewModel review = MongoUtil.GetReview(userId, new ObjectId(storyId));
            UserModel user = MongoUtil.GetUser(userId);

            if (review != null)
            {
                return Ok(review.getReviewApiModel(user.Name, user.Avatar));
            }

            return Ok(new ReviewModel { Rating = 0, Opinion = "" });
        }


        [HttpPut("edit/{storyId}")]
        public IActionResult EditReview([FromRoute] String storyId, [FromHeader(Name = "Authorization")] String userToken, [FromBody] ReviewApiModel reviewApiModel)
        {
            MongoUtil.EditReview(new ObjectId(JwtUtil.GetUserIdFromToken(userToken)), 
                                new ObjectId(storyId), 
                                reviewApiModel.Rating, 
                                reviewApiModel.Opinion, 
                                DateTime.Now);

            return Ok();
        }

        [HttpDelete("delete/{storyId}"), Authorize]
        public IActionResult DeleteReview([FromRoute] String storyId, [FromHeader(Name = "Authorization")] String userToken)
        {
            MongoUtil.DeleteReview(new ObjectId(JwtUtil.GetUserIdFromToken(userToken)), 
                                new ObjectId(storyId));

            return Ok();
        }
    }
}
