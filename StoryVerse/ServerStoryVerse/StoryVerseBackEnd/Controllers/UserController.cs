using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;
using Google.Apis.Auth;
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
        private static ConcurrentDictionary<string, string> verificationCodes = new ConcurrentDictionary<string, string>();

        #region User Entity

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

        [HttpDelete("delete-account"), Authorize]
        public IActionResult DeleteAccount([FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            MongoUtil.DeleteUser(userId);
            return Ok("User account and related data deleted successfully");
        }

        [HttpPatch("update-name"), Authorize]
        public IActionResult UpdateName([FromBody] UserApiModel updatedUser, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            MongoUtil.UpdateUserName(userId, updatedUser.Name, updatedUser.Surname);
            return Ok("User name updated successfully");
        }


        [HttpGet("who-i-am"), Authorize]
        public IActionResult WhoIAm([FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            UserModel um = MongoUtil.GetUser(userId);
            UserApiModel uam = um.getUserApiModel();
            uam.Password = "";
            uam.Birthday = um.Birthday.ToShortDateString();
            uam.Avatar = um.Avatar;

            return Ok(uam);
        }

        [HttpPatch("update-avatar"), Authorize]
        public IActionResult UpdateAvatar([FromBody] UserApiModel updatedUser, [FromHeader(Name = "Authorization")] string token)
        {
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken(token));
            MongoUtil.UpdateUserAvatar(userId, updatedUser.Avatar);
            return Ok("User avatar updated successfully");
        }

        [HttpPost("send-verification-code")]
        public IActionResult SendVerificationCode([FromBody] EmailModel model)
        {
            var email = model.Email;
            UserModel user = MongoUtil.GetUser(email);
            if (user != null)
            {
                return Conflict("Email already used");
            }

            // Generate verification code
            var verificationCode = new Random().Next(100000, 999999).ToString();

            // Store the code temporarily
            verificationCodes[email] = verificationCode;

            var smtpClient = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                Credentials = new NetworkCredential(Environment.GetEnvironmentVariable("SmtpUserName"), Environment.GetEnvironmentVariable("SmtpPassword"))
            };

            using (var message = new MailMessage(new MailAddress(Environment.GetEnvironmentVariable("SmtpUserName"), "StoryVerse"), new MailAddress(email))
            {
                Subject = "Verification Code",
                Body = "Your verification code is " + verificationCode
            })
            {
                smtpClient.Send(message);
            }

            return Ok("Verification code sent");
        }

        [HttpPost("verify-code-and-signup")]
        public IActionResult VerifyCodeAndSignup([FromBody] VerifyCodeAndSignupModel model)
        {
            if (!verificationCodes.TryGetValue(model.User.Email, out var storedCode) || storedCode != model.VerificationCode)
            {
                return BadRequest("Invalid verification code or email");
            }

            verificationCodes.TryRemove(model.User.Email, out _);

            model.User.RegisteredStories = new List<ObjectId>();

            MongoUtil.AddUser(model.User);

            return Ok("User created successfully");
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
                    Subject = "Account Password",
                    Body = "Hello, " + user.Surname + " " + user.Name + ",\n\n" + Environment.NewLine + Environment.NewLine + "Your account password is " + user.Password + Environment.NewLine + Environment.NewLine + "Do not reply to this email address. It is used only for automated messages!"
                })
                {
                    smtpClient.Send(message);
                }

                return Ok("The password has been sent to your email");
            }

            return Ok("The entered email address is not found in the database!");
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
                .ConvertAll(new Converter<StoryModel, StoryApiModel>(e =>
                {
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
                .ConvertAll(new Converter<StoryModel, StoryApiModel>(e =>
                {
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