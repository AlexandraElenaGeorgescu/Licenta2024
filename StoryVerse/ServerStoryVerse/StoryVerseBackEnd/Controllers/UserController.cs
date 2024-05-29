using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using StoryVerseBackEnd.Models;
using StoryVerseBackEnd.Utils;


namespace StoryVerseBackEnd.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        #region User Entity

        [HttpPost("signup")]
        public IActionResult Signup([FromBody] UserApiModel userApiModel)
        {
            if (MongoUtil.GetUser(userApiModel.getUserModel(ModelsExtensionMethods.zeroId).Email) == null)
            {
                MongoUtil.AddUser(userApiModel.getUserModel(ModelsExtensionMethods.zeroId));
                return Ok("Success");
            }

            return Conflict("Email used");
        }

        [HttpPost("signin")]
        public IActionResult Signin([FromBody] UserApiModel userApiModel)
        {
            UserModel user = MongoUtil.GetUser(userApiModel.Email, userApiModel.Password);

            if (user != null)
            {
                string token = JwtUtil.getToken(user.Id.ToString(), user.Email, DateTime.Now.AddMinutes(30));
                return Ok(token);
            }

            return BadRequest("Invalid email or password!");
        }

        [HttpGet("who-i-am"),Authorize]
        public IActionResult WhoIAm([FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            UserModel um = MongoUtil.GetUser(userId);
            UserApiModel uam = um.getUserApiModel();
            uam.Password = "";
            uam.Birthday = "";

            return Ok(uam);
        }

        [HttpGet("send-password/{email}")]
        public IActionResult SendPasswordThroughEmail([FromRoute] string email)
        {
            UserModel user = MongoUtil.GetUser(email);

            if (user != null)
            {
                var smtpClient = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    Credentials = new NetworkCredential(Environment.GetEnvironmentVariable("SmtpUserName"), Environment.GetEnvironmentVariable("SmtpPassword"))
                };

                using (var message = new MailMessage(new MailAddress(Environment.GetEnvironmentVariable("SmtpUserName"), "StoryVerse"), new MailAddress(user.Email))
                {
                    Subject = "Parolă cont",
                    Body = "Salut, " + user.Surname + " " + user.Name + ",\n\n" + Environment.NewLine + Environment.NewLine + "Parola contului tău este " + user.Password + Environment.NewLine + Environment.NewLine + "Nu răspunde acestei adrese de email. Este folosită doar pentru mesaje automate!"
                })
                {
                    smtpClient.Send(message);
                }

                return Ok("Parola a fost trimisă pe email");
            }

            return Ok("Adresa de email introdusă nu se regăsește în baza de date!");
        }

        [HttpPatch("change-password"), Authorize]
        public IActionResult ChangePassword([FromBody] UserApiModel newPass, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));

            MongoUtil.ChangePassword(userId, newPass.Password);

            return Ok();
        }

        #endregion

        #region User Stories Info

        [HttpGet("registered-stories/{pageSize}/{pageId}"), Authorize]
        public IActionResult Registered([FromRoute] int pageSize, [FromRoute] int pageId, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            return Ok(MongoUtil.GetRegisteredStories(userId, pageSize, pageId)
                .ConvertAll(new Converter<StoryModel, StoryApiModel>(e => {
                    return e.getstoryApiModel();
                })));
        }

        [HttpGet("reviewed-stories/{pageSize}/{pageId}"), Authorize]
        public IActionResult Reviewed([FromRoute] int pageSize, [FromRoute] int pageId, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            List<ReviewModel> reviews = MongoUtil.GetUserReviews(userId, pageSize, pageId);
            List<StoryApiModel> Stories = new List<StoryApiModel>();

            foreach (ReviewModel review in reviews)
            {
                Stories.Add(MongoUtil.GetStory(review.StoryId).getstoryApiModel());
            }

            return Ok(Stories);
        }

        [HttpGet("created-stories/{pageSize}/{pageId}"), Authorize]
        public IActionResult Created([FromRoute] int pageSize, [FromRoute] int pageId, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            return Ok(MongoUtil.GetCreatedStories(userId, pageSize, pageId)
                .ConvertAll(new Converter<StoryModel, StoryApiModel>(e => {
                    return e.getstoryApiModel();
                })));
        }

        #endregion

        #region User Story Status

        [HttpGet("registration-status/{storyId}"), Authorize]
        public IActionResult RegistrationStatus([FromRoute] string storyId, [FromHeader(Name = "Authorization")] string userToken)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(userToken));
            String status = MongoUtil.GetRegistrationStatus(new ObjectId(storyId), userId);
            return Ok(status);
        }

        [HttpPatch("register/{storyId}"), Authorize]
        public IActionResult RegisterUserToStory([FromRoute] string storyId, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            Boolean ok = MongoUtil.RegisterUserToStory(new ObjectId(storyId), userId);
            return Ok(ok);
        }

        [HttpPatch("unregister/{storyId}"), Authorize]
        public IActionResult UnregisterUserToStory([FromRoute] string storyId, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            Boolean ok = MongoUtil.UnregisterUserFromStory(new ObjectId(storyId), userId);
            MongoUtil.DeleteReview(userId, new ObjectId(storyId));
            return Ok(ok);
        }

        #endregion
    }
}