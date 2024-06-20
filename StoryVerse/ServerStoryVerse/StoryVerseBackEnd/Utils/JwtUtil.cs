using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace StoryVerseBackEnd.Utils
{
    public class JwtUtil
    {
        public static string getToken(String userId, String userEmail, DateTime tokenExpirationDate)
        {
            SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
            SigningCredentials signinCred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = new JwtSecurityToken(claims: new List<Claim> {
                                                                    new Claim("Id", userId),
                                                                    new Claim(ClaimTypes.Email, userEmail)},
                                                         issuer: "StoryVerse Server",
                                                         audience: "StoryVerse Client",
                                                        expires: tokenExpirationDate,
                                                        signingCredentials: signinCred);

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return tokenString;
        }

        public static String GetUserIdFromToken(String userToken)
        {
            string x = userToken.Substring(7);
            JwtSecurityTokenHandler jwtsth = new JwtSecurityTokenHandler();
            bool y = jwtsth.CanReadToken(x);
            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadToken(userToken.Substring(7)) as JwtSecurityToken;
            String userId = token.Claims.FirstOrDefault(claim => claim.Type == "Id").Value;
            return userId;
        }

        public static void setSecurityKey(string key)
        {
            _key = key;
        }

        private static string _key = "";
    }
}
