﻿using System;
using System.Windows.Data;
using Dulcet.Twitter;
using Inscribe.Common;
using Inscribe.Configuration;
using Inscribe.Configuration.Settings;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;

namespace Mystique.Views.Converters.Particular
{
    public enum UserNameViewKind
    {
        ScreenName,
        Name,
        ViewName,
        NotifyViewName,
        RetweetedScreenName,
        DirectMessageTarget
    }

    public class TVMToUserNameConverter : OneWayConverter<TweetViewModel, string>
    {

        public override string ToTarget(TweetViewModel input, object parameter)
        {
            if (input == null) return String.Empty;
            UserNameViewKind kind;
            if (!Enum.TryParse(parameter as string, out kind))
                kind = UserNameViewKind.ScreenName;
            switch (kind)
            {
                case UserNameViewKind.Name:
                    return UserName(input);
                case UserNameViewKind.ScreenName:
                    return ScreenName(input);
                case UserNameViewKind.RetweetedScreenName:
                    if (input == null) return String.Empty;
                    return input.Status.User.ScreenName;
                case UserNameViewKind.ViewName:
                    switch (Setting.Instance.TweetExperienceProperty.UserNameViewMode)
                    {
                        case NameView.ID:
                            return ScreenName(input);
                        case NameView.Name:
                            return UserName(input);
                        case NameView.Both:
                        default:
                            return ScreenName(input) + " (" + UserName(input) + ")";
                    }
                case UserNameViewKind.NotifyViewName:
                    switch (Setting.Instance.TweetExperienceProperty.NotificationNameViewMode)
                    {
                        case NameView.ID:
                            return ScreenName(input);
                        case NameView.Name:
                            return UserName(input);
                        case NameView.Both:
                        default:
                            return ScreenName(input) + " (" + UserName(input) + ")";
                    }
                case UserNameViewKind.DirectMessageTarget:
                    if (input == null || !(input.Status is TwitterDirectMessage)) return String.Empty;
                    return ((TwitterDirectMessage)input.Status).Recipient.ScreenName ?? String.Empty;

                default:
                    return String.Empty;
            }
        }

        private string UserName(TweetViewModel status)
        {
            if (status == null) return String.Empty;
            return TwitterHelper.GetSuggestedUser(status).UserName ?? String.Empty;
        }

        private string ScreenName(TweetViewModel status)
        {
            if (status == null) return String.Empty;
            return TwitterHelper.GetSuggestedUser(status).ScreenName ?? String.Empty;
        }
    }
}
