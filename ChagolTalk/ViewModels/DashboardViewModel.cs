using ChagolTalk.Models.Identity;

namespace ChagolTalk.ViewModels
{
    public class DashboardViewModel
    {
        public ApplicationUser User { get; set; } = null!;

        public int OnlineCount { get; set; }

        public int WaitingCount { get; set; }

        public int RecentConversationCount { get; set; }

        public string FormattedVoiceTime
        {
            get
            {
                var seconds = User.TotalVoiceSeconds;
                var hours = seconds / 3600;
                var minutes = (seconds % 3600) / 60;

                if (hours > 0)
                    return $"{hours}h {minutes}m";

                return $"{minutes}m";
            }
        }
    }
}
