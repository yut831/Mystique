﻿using System;
using System.Net;
using System.Threading;
using Dulcet.Twitter.Streaming;
using Inscribe.Configuration;
using Inscribe.Model;
using Inscribe.Storage;
using Inscribe.Threading;

namespace Inscribe.Communication.MainTimeline.Connection
{
    public class UserStreamsConnection : IDisposable
    {

        #region Static Variables and Methods 

        static StreamingCore streamCore;
        static Thread streamPump;
        static UserStreamsConnection()
        {
            streamCore = new StreamingCore();
            streamCore.OnExceptionThrown += new Action<Exception>(streamCore_OnExceptionThrown);
            ThreadHelper.Halt += () => streamPump.Abort();
            ThreadHelper.Halt += () => streamCore.Dispose();
            streamPump = new Thread(PumpTweets);
            streamPump.Start();
        }

        static void streamCore_OnExceptionThrown(Exception obj)
        {
            NotifyStorage.Notify(obj.Message);
        }

        /// <summary>
        /// ツイートのポンピングスレッド
        /// </summary>
        private static void PumpTweets()
        {
            try
            {
                foreach (var s in streamCore.EnumerateStreamingElements())
                {
                    try
                    {
                        RegisterStreamElement(s.Item1 as AccountInfo, s.Item2);
                    }
                    catch (ThreadAbortException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        NotifyStorage.Notify(e.Message);
                    }
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception e)
            {
                NotifyStorage.Notify(e.Message);
            }
        }

        /// <summary>
        /// ストリームエレメントを処理します。
        /// </summary>
        /// <param name="info">ストリームエレメントを受信したアカウント</param>
        /// <param name="elem">ストリームエレメント</param>
        private static void RegisterStreamElement(AccountInfo info, StreamingEvent elem)
        {
            switch (elem.Kind)
            {
                case ElementKind.Status:
                    // 通常ステータスを受信した
                    TweetStorage.Register(elem.Status);
                    break;
                case ElementKind.Favorite:
                    TweetStorage.Register(elem.Status);
                    var avm = TweetStorage.Get(elem.Status.Id);
                    avm.RegisterFavored(UserStorage.Get(elem.SourceUser));
                    // TODO: Notify Event
                    break;
                case ElementKind.Unfavorite:
                    TweetStorage.Register(elem.Status);
                    var rvm = TweetStorage.Get(elem.Status.Id);
                    rvm.RemoveFavored(UserStorage.Get(elem.SourceUser));

                    // TODO: Notify Event
                    break;
                case ElementKind.Delete:
                    TweetStorage.Remove(elem.DeletedStatusId);
                    break;
                case ElementKind.ListUpdated:
                case ElementKind.ListMemberAdded:
                case ElementKind.ListMemberRemoved:
                case ElementKind.ListSubscribed:
                case ElementKind.ListUnsubscribed:
                    // TODO: do something
                    break;
                case ElementKind.Follow:
                case ElementKind.Unfollow:
                    var affect = AccountStorage.Get(elem.SourceUser.ScreenName);
                    var effect = AccountStorage.Get(elem.TargetUser.ScreenName);
                    if (affect != null)
                    {
                        // Add/Remove followings
                        if (elem.Kind == ElementKind.Follow)
                            affect.RegisterFollowing(UserStorage.Get(elem.TargetUser));
                        else
                            affect.RemoveFollowing(UserStorage.Get(elem.TargetUser));
                    }
                    if (effect != null)
                    {
                        // Add/Remove followers
                        if (elem.Kind == ElementKind.Follow)
                            effect.RegisterFollower(UserStorage.Get(elem.SourceUser));
                        else
                            effect.RemoveFollower(UserStorage.Get(elem.SourceUser));
                    }
                    // TODO: Notify events
                    break;
                case ElementKind.Blocked:
                    if (info == null) break;
                    info.RemoveFollowing(UserStorage.Get(elem.TargetUser));
                    info.RemoveFollower(UserStorage.Get(elem.TargetUser));
                    info.RegisterBlocking(UserStorage.Get(elem.TargetUser));
                    // TODO: notify events
                    break;
                case ElementKind.Unblocked:
                    if (info == null) break;
                    info.RemoveBlocking(UserStorage.Get(elem.TargetUser));
                    // TODO: Notify events
                    break;
            }
        }

        public static UserStreamsConnection Connect(AccountInfo account, string[] queries)
        {
            if (streamCore == null)
                throw new InvalidOperationException("Stream core is not initialized.");
            return new UserStreamsConnection(account, queries);
        }

        #endregion // Static Variables and Methods

        private StreamingConnection connection = null;

        private UserStreamsConnection(AccountInfo account, string[] queries)
        {
            string track = null;
            if (queries != null && queries.Length > 0)
            {
                track = String.Join(",", queries);
            }

            try
            {
                Connect(account, track);
            }
            catch (ThreadAbortException) { }
            catch (Exception e)
            {
                throw new Exception("@" + account.ScreenName + ": 接続できませんでした。", e);
            }
        }

        /// <summary>
        /// ストリーミングAPIへ接続します。<para />
        /// 接続に失敗した場合、規定アルゴリズムに沿って自動で再接続を試みます。<para />
        /// それでも失敗する場合はfalseを返します。この場合、再接続を試みてはいけません。
        /// </summary>
        /// <param name="track">トラック対象キーワード</param>
        private void Connect(AccountInfo info, string track)
        {
            try
            {
                int failureCounter = 0;
                while (true)
                {
                    // 接続
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Start connection User Streams with track:" + track);
                        connection = streamCore.ConnectNew(
                            info, StreamingDescription.ForUserStreams(
                            track: track, repliesAll: info.AccoutProperty.UserStreamsRepliesAll));
                    }
                    catch (WebException we)
                    {
                        if ((int)we.Status == 420)
                        {
                            // 多重接続されている
                            throw new WebException("@" + info.ScreenName + ": 多重接続されています。接続できません。");
                        }

                        if (we.Status == WebExceptionStatus.Timeout) // タイムアウト例外なら再試行する
                        {
                            NotifyStorage.Notify("@" + info.ScreenName + ": User Streams接続がタイムアウトしました。再試行します...");
                        }
                        else
                        {
                            string descText = we.Status.ToString();
                            if (we.Status == WebExceptionStatus.ProtocolError)
                            {
                                var hwr = we.Response as HttpWebResponse;
                                if (hwr != null)
                                {
                                    descText += " - " + hwr.StatusCode + " : " + hwr.StatusDescription;
                                }
                            }
                            throw new WebException("接続に失敗しました。(" + descText + ")");
                        }
                    }

                    if (connection != null)
                    {
                        // Connection succeed
                        return;
                    }

                    if (failureCounter > 0)
                    {
                        if (failureCounter > Setting.Instance.ConnectionProperty.UserStreamsConnectionFailedMaxWaitSec)
                        {
                            throw new WebException("User Streamsへの接続に失敗しました。");
                        }
                        else
                        {
                            NotifyStorage.Notify("@" + info.ScreenName + ": User Streamsへの接続に失敗。再試行まで" + (failureCounter / 1000).ToString() + "秒待機...", failureCounter / 1000);

                            // ウェイトカウント
                            Thread.Sleep(failureCounter);
                            NotifyStorage.Notify("@" + info.ScreenName + ": User Streamsへの接続を再試行します...");
                        }
                    }
                    else
                    {
                        NotifyStorage.Notify("@" + info.ScreenName + ": User Streamsへの接続に失敗しました。再試行します...");
                    }

                    // 最初に失敗したらすぐに再接続
                    // ２回目以降はその倍に増やしていく
                    // 300を超えたら接続失敗で戻る
                    if (failureCounter == 0)
                    {
                        failureCounter = Setting.Instance.ConnectionProperty.UserStreamsConnectionFailedInitialWaitSec;
                    }
                    else
                    {
                        failureCounter *= 2;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new WebException("User Streamsへの接続に失敗しました。");
            }
        }
        
        /// <summary>
        /// 接続が切断されました。
        /// </summary>
        public event Action<bool> OnDisconnected = _ => { };

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UserStreamsConnection()
        {
            Dispose(false);
        }

        bool disposed = false;
        private void Dispose(bool disposing)
        {
            if (this.disposed) return;
            this.disposed = true;
        }
    }
}