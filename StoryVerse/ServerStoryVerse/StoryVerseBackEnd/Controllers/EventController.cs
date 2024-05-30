using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using StoryVerseBackEnd.Models;
using StoryVerseBackEnd.Utils;

namespace StoryVerseBackEnd.Controllers
{
    [Route("api/story")]
    [ApiController]
    public class storyController : ControllerBase
    {
        [HttpGet("pie-chart/{storyId}"), Authorize]
        public IActionResult GetPieChartData([FromHeader(Name = "Authorization")] string token, [FromRoute] string storyId)
        {
            string userId = JwtUtil.GetUserIdFromToken(token);
            StoryModel ev = MongoUtil.GetStory(new ObjectId(storyId));

            if (ev.CreatorId == new ObjectId(userId))
                return Ok(MongoUtil.countRegistrations(new ObjectId(storyId)));

            return Unauthorized();
        }

        [HttpGet("line-chart/{storyId}"), Authorize]
        public IActionResult GetLineChartData([FromHeader(Name = "Authorization")] string token, [FromRoute] string storyId)
        {
            string userId = JwtUtil.GetUserIdFromToken(token);
            StoryModel ev = MongoUtil.GetStory(new ObjectId(storyId));

            if (ev.CreatorId == new ObjectId(userId))
                return Ok(MongoUtil.countMsgs(new ObjectId(storyId)));

            return Unauthorized();
        }

        [HttpGet("reviews-stats/{storyId}"), Authorize]
        public IActionResult GetReviewsStats([FromHeader(Name = "Authorization")] string token, [FromRoute] string storyId)
        {
            string userId = JwtUtil.GetUserIdFromToken(token);
            StoryModel ev = MongoUtil.GetStory(new ObjectId(storyId));

            if(ev.CreatorId == new ObjectId(userId))
                return Ok(MongoUtil.getReviewsStats(new ObjectId(storyId)));

            return Unauthorized();
        }

        [HttpPost("create"), Authorize]
        public IActionResult Create([FromHeader(Name = "Authorization")] string token, [FromBody] StoryApiModel storyApiModel)
        {
            String userId = JwtUtil.GetUserIdFromToken(token);
            StoryModel storyModel = storyApiModel.getStoryModel(userId, DateTime.Now);
            storyModel.Image = "StaticFiles/Images/standard.jpg";
            MongoUtil.AddStory(storyModel);

            return Ok("Story created");
        }

        [HttpPost("upload-image/{storyId}"), DisableRequestSizeLimit, Authorize]
        public IActionResult Upload([FromHeader(Name = "Authorization")] string token)
        {
            try
            {
                String userId = JwtUtil.GetUserIdFromToken(token);
                var file = Request.Form.Files[0];
                var folderName = Path.Combine("StaticFiles", "Images");
                var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), folderName);

                if (file.Length > 0)
                {
                    var fileName = userId + "_" + DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() + ".jpg";
                    var fullPath = Path.Combine(pathToSave, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }

                    List<StoryModel> storyList = MongoUtil.GetCreatedStories(new ObjectId(userId), 1, 0);
                    if (storyList.Count > 0 && storyList[0].Image == "StaticFiles/Images/standard.jpg")
                    {
                        string newImage = "StaticFiles/Images/" + fileName;
                        MongoUtil.UpdateImage(storyList[0].Id, newImage);
                    }

                    return Ok("Image updated");
                }
                else
                {
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("details/{storyId}")]
        public IActionResult Details([FromRoute] string storyId)
        {
            StoryModel storyModel = MongoUtil.GetStory(new ObjectId(storyId));
            return Ok(storyModel.getstoryApiModel());
        }

        [HttpGet("browse/{pageSize}/{pageId}")]
        public IActionResult Browse([FromRoute] int pageSize, [FromRoute] int pageId, [FromQuery] string genre = "")
        {
            return Ok(MongoUtil.GetStories(pageSize, pageId, genre)
                .ConvertAll(new Converter<StoryModel, StoryApiModel>(storyModel => {
                    return storyModel.getstoryApiModel();
                })));
        }

        [HttpGet("search/{pageSize}/{pageId}/{searchText}")]
        public IActionResult Search([FromRoute] int pageSize, [FromRoute] int pageId, [FromRoute] String searchText)
        {   
            return Ok(MongoUtil.Search(pageSize, pageId, searchText)
                .ConvertAll(new Converter<StoryModel, StoryApiModel>(storyModel => {
                    return storyModel.getstoryApiModel();
                })));
        }

        [HttpGet("chat-messages/{storyId}")]
        public IActionResult GetMessages([FromRoute] String storyId, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId reqUserId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            List<MessageModel> messages = MongoUtil.GetMessages(new ObjectId(storyId));
            List<MessageApiModel> apiMessages = messages.ConvertAll(new Converter<MessageModel, MessageApiModel>(msg => {
                ObjectId pubUserId = msg.UserId;
                if (pubUserId == reqUserId)
                    return msg.getMessageApiModel();
                msg.DateSent = msg.DateSent.AddHours(3);
                return msg.getMessageApiModel(MongoUtil.GetUser(pubUserId).Name);
            }));

            return Ok(apiMessages);
        }
    }
}