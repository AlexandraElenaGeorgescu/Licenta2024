namespace StoryVerseBackEnd.Models
{
    public class VerifyCodeAndSignupModel
    {
        public string VerificationCode { get; set; }
        public UserModel User { get; set; }
    }
}
