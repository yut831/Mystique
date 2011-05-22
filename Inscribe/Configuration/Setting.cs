﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Inscribe.Common;
using Inscribe.Configuration.Settings;
using System.ComponentModel;
using Inscribe.Storage;

namespace Inscribe.Configuration
{
    /// <summary>
    /// 設定の保持クラス
    /// </summary>
    public class Setting
    {
        private static Setting _instance = null;

        private static string settingFilePath
        {
            get { return Path.Combine(Path.GetDirectoryName(Define.GetExecutingPath()), Define.SettingFileName); }
        }

        public static Setting Instance
        {
            get
            {
                if (_instance == null)
                {
#if DEBUG
                    _instance = new Setting();
#else
                    throw new InvalidOperationException("設定システムはまだ初期化されていません。");
#endif
                }
                return _instance;
            }
        }

        public static bool IsInitialized
        {
            get
            {
                return _instance != null;
            }
        }

        public static void Initialize()
        {
            if (_instance == null)
            {
                try
                {
                    _instance = XMLSerializer.LoadXML<Setting>(settingFilePath, true);
                }
                catch (Exception)
                {
                    _instance = new Setting();
                }
            }
            else
            {
                throw new InvalidOperationException("すでに初期化されています。");
            }
        }

        public static event Action SettingValueChanged = () => { };

        public static void RaiseSettingValueChanged()
        {
            SettingValueChanged();
            // TweetModel.RaiseInvalidateAllCache();
        }

        public Setting()
        {
            this.KernelProperty = new KernelProperty();
            this.ConnectionProperty = new ConnectionProperty();
            this.AccountProperty = new AccountProperty();
            this.ExperienceProperty = new ExperienceProperty();
            this.TimelineExperienceProperty = new Settings.TimelineExperienceProperty();
            this.TweetExperienceProperty = new Settings.TweetExperienceProperty();
            this.InputExperienceProperty = new InputExperienceProperty();
            this.TimelineFilteringProperty = new TimelineFilterlingProperty();
            this.ExternalServiceProperty = new ExternalServiceProperty();
            this.ColoringProperty = new ColoringProperty();
            this.NotificationProperty = new Settings.NotificationProperty();
            this.StateProperty = new StateProperty();
            this.TwitterProperty = new TwitterProperty();
        }

        public KernelProperty KernelProperty { get; set; }

        public ConnectionProperty ConnectionProperty { get; set; }

        /// <summary>
        /// エディタ内から参照しないでください。<para />
        /// KernelModel.AccountServerModelから取得してください。
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public AccountProperty AccountProperty { get; set; }

        public ExperienceProperty ExperienceProperty { get; set; }

        public TimelineExperienceProperty TimelineExperienceProperty { get; set; }

        public TweetExperienceProperty TweetExperienceProperty { get; set; }

        public InputExperienceProperty InputExperienceProperty { get; set; }

        public TimelineFilterlingProperty TimelineFilteringProperty { get; set; }

        public ExternalServiceProperty ExternalServiceProperty { get; set; }

        public ColoringProperty ColoringProperty { get; set; }

        public NotificationProperty NotificationProperty { get; set; }

        public StateProperty StateProperty { get; set; }

        public TwitterProperty TwitterProperty { get; set; }

        public void Save()
        {
            var temp = Path.GetTempFileName();
            try
            {
                XMLSerializer.SaveXML<Setting>(temp, this);
                File.Delete(settingFilePath);
                File.Move(temp, settingFilePath);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                NotifyStorage.Notify("設定保存ができません:" + e.Message);
            }
        }
    }
}