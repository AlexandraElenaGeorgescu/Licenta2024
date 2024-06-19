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
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        [HttpGet("notifications"), Authorize]
        public IActionResult GetNotifications([FromHeader(Name = "Authorization")] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized();
            }

            ObjectId userId;
            try
            {
                userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            }
            catch
            {
                return Unauthorized();
            }

            var notifications = MongoUtil.GetNotifications(userId);
            return Ok(notifications);
        }

        [HttpPatch("notifications/mark-read/{id}"), Authorize]
        public IActionResult MarkNotificationAsRead([FromRoute] string id, [FromHeader(Name = "Authorization")] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized();
            }

            ObjectId userId;
            try
            {
                userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            }
            catch
            {
                return Unauthorized();
            }

            MongoUtil.MarkNotificationAsRead(new ObjectId(id), userId);
            return Ok(new { message = "Notification marked as read" });
        }
    }
}
