﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Inscribe.Configuration;
using Inscribe.Filter;
using Inscribe.Filter.Filters.Numeric;
using Inscribe.Subsystems;
using Inscribe.ViewModels.Behaviors.Messaging;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;
using Livet;
using Livet.Messaging;
using System.Threading.Tasks;
using Inscribe.Common;

namespace Inscribe.ViewModels.PartBlocks.MainBlock
{
    public class ColumnOwnerViewModel : ViewModel
    {
        #region Closed tab stack control

        private object _ctsLock = new object();

        private Stack<TabViewModel> _closedTabStacks = new Stack<TabViewModel>();

        /// <summary>
        /// 閉じタブスタックにタブをプッシュします。<para />
        /// IsAliveのステート変更は行いますが、クエリやリストの購読解除は呼び出し側で行う必要があります。
        /// </summary>
        public void PushClosedTabStack(TabViewModel viewmodel)
        {
            // タブを殺す
            viewmodel.IsAlive = false;
            lock (this._ctsLock)
            {
                this._closedTabStacks.Push(viewmodel);
            }
            this._columns.ForEach(c => c.RebirthTabCommand.RaiseCanExecuteChanged());
        }

        /// <summary>
        /// 閉じタブスタックからタブをポップします。<para />
        /// IsAliveのステート変更は行いますが、クエリやリストの再購読は呼び出し側で行う必要があります。
        /// </summary>
        public TabViewModel PopClosedTab()
        {
            TabViewModel ret;
            lock (this._ctsLock)
            {
                ret = this._closedTabStacks.Pop();
            }
            this._columns.ForEach(c => c.RebirthTabCommand.RaiseCanExecuteChanged());
            // タブをブッ生き返す
            ret.IsAlive = true;
            return ret;
        }

        /// <summary>
        /// 閉じタブスタックにタブが存在するかを返します。
        /// </summary>
        public bool IsExistedClosedTab()
        {
            lock (this._ctsLock)
            {
                return this._closedTabStacks.Count > 0;
            }
        }

        /// <summary>
        /// 閉じタブスタックをクリアします。
        /// </summary>
        public void ClearClosedTab()
        {
            lock (this._ctsLock)
            {
                this._closedTabStacks.Clear();
            }
            this._columns.ForEach(c => c.RebirthTabCommand.RaiseCanExecuteChanged());
        }

        #endregion

        public MainWindowViewModel Parent { get; private set; }

        public ColumnOwnerViewModel(MainWindowViewModel parent)
        {
            this.Parent = parent;
            this.CurrentFocusColumn = CreateColumn();
            Setting.Instance.StateProperty.TabPropertyProvider = () => Columns.Select(cvm => cvm.TabItems.Select(s => s.TabProperty));
            RegisterKeyAssign();
        }


        private ObservableCollection<ColumnViewModel> _columns = new ObservableCollection<ColumnViewModel>();
        public ObservableCollection<ColumnViewModel> Columns { get { return this._columns; } }

        public int ColumnIndexOf(ColumnViewModel vm)
        {
            return this._columns.IndexOf(vm);
        }

        public ColumnViewModel CreateColumn(int index = -1)
        {
            var nvm = new ColumnViewModel(this);
            if (index == -1 || index >= this._columns.Count)
                this._columns.Add(nvm);
            else
                this._columns.Insert(index, nvm);
            // ColumnViewModelのイベントを購読。
            // このイベントはリリースの必要がない
            nvm.GotFocus += (o, e) => CurrentFocusColumn = nvm;
            nvm.SelectedTabChanged += _ => RaisePropertyChanged(() => CurrentTab);
            return nvm;
        }

        /// <summary>
        /// タブが一つも含まれないカラムを削除します。
        /// </summary>
        public void GCColumn()
        {
            this._columns.Where(c => c.TabItems.Count() == 0).ToArray()
                .ForEach(v => this._columns.Remove(v));
            // 少なくとも1つのカラムは必要
            if (this._columns.Count == 0)
                CreateColumn();
        }

        private ColumnViewModel _currentFocusColumn = null;
        public ColumnViewModel CurrentFocusColumn
        {
            get { return this._currentFocusColumn; }
            protected set
            {
                this._currentFocusColumn = value;
                RaisePropertyChanged(() => CurrentFocusColumn);
                RaisePropertyChanged(() => CurrentTab);
                this._columns.SelectMany(c => c.TabItems).ForEach(vm => vm.UpdateIsCurrentFocused());
                var cfc = this.CurrentColumnChanged;
                if (cfc != null)
                    cfc(value);
                var ctab = this.CurrentTabChanged;
                if (ctab != null)
                    ctab(value.SelectedTabViewModel);
            }
        }

        public event Action<ColumnViewModel> CurrentColumnChanged;

        public TabViewModel CurrentTab
        {
            get
            {
                if (this.CurrentFocusColumn == null)
                    return null;
                return this.CurrentFocusColumn.SelectedTabViewModel;
            }
        }

        public event Action<TabViewModel> CurrentTabChanged;

        public void MoveFocusTo(ColumnViewModel column, ColumnsLocation moveTarget)
        {
            if (_columns.Count == 0)
                throw new InvalidOperationException("No columns existed.");
            int i = _columns.IndexOf(column);
            if (i == -1)
                return;
            switch (moveTarget)
            {
                case ColumnsLocation.Next:
                    if (i == _columns.Count - 1)
                        _columns[0].SetFocus();
                    else
                        _columns[i + 1].SetFocus();
                    break;
                case ColumnsLocation.Previous:
                    if (i == 0)
                        _columns[_columns.Count - 1].SetFocus();
                    else
                        _columns[i - 1].SetFocus();
                    break;
            }
        }

        public void SetFocus()
        {
            this.CurrentFocusColumn.SetFocus();
        }

        #region KeyAssignCore

        public void RegisterKeyAssign()
        {
            // Moving focus
            KeyAssignCore.RegisterOperation("FocusToTimeline", this.SetFocus);
            KeyAssignCore.RegisterOperation("MoveLeft", () => MoveHorizontal(false, false));
            KeyAssignCore.RegisterOperation("MoveLeftColumn", () => MoveHorizontal(false, true));
            KeyAssignCore.RegisterOperation("MoveRight", () => MoveHorizontal(true, false));
            KeyAssignCore.RegisterOperation("MoveRightColumn", () => MoveHorizontal(true, true));
            KeyAssignCore.RegisterOperation("MoveDown", () => MoveVertical(ListSelectionKind.SelectBelow));
            KeyAssignCore.RegisterOperation("MoveUp", () => MoveVertical(ListSelectionKind.SelectAbove));
            KeyAssignCore.RegisterOperation("MoveTop", () => MoveVertical(ListSelectionKind.SelectFirst));
            KeyAssignCore.RegisterOperation("MoveBottom", () => MoveVertical(ListSelectionKind.SelectLast));
            KeyAssignCore.RegisterOperation("Deselect", () => MoveVertical(ListSelectionKind.Deselect));

            // Moving focus (additional)
            KeyAssignCore.RegisterOperation("Select1stTab", () => SelectIndexOfTab(0));
            KeyAssignCore.RegisterOperation("Select2ndTab", () => SelectIndexOfTab(1));
            KeyAssignCore.RegisterOperation("Select3rdTab", () => SelectIndexOfTab(2));
            KeyAssignCore.RegisterOperation("Select4thTab", () => SelectIndexOfTab(3));
            KeyAssignCore.RegisterOperation("Select5thTab", () => SelectIndexOfTab(4));
            KeyAssignCore.RegisterOperation("Select6thTab", () => SelectIndexOfTab(5));
            KeyAssignCore.RegisterOperation("Select7thTab", () => SelectIndexOfTab(6));
            KeyAssignCore.RegisterOperation("Select8thTab", () => SelectIndexOfTab(7));
            KeyAssignCore.RegisterOperation("Select9thTab", () => SelectIndexOfTab(8));
            KeyAssignCore.RegisterOperation("Select10thTab", () => SelectIndexOfTab(9));
            KeyAssignCore.RegisterOperation("SelectNthTab", s => SelectIndexOfTab(Int32.Parse(s)));

            KeyAssignCore.RegisterOperation("Select1stColumn", () => SelectIndexOfColumn(0));
            KeyAssignCore.RegisterOperation("Select2ndColumn", () => SelectIndexOfColumn(1));
            KeyAssignCore.RegisterOperation("Select3rdColumn", () => SelectIndexOfColumn(2));
            KeyAssignCore.RegisterOperation("Select4thColumn", () => SelectIndexOfColumn(3));
            KeyAssignCore.RegisterOperation("Select5thColumn", () => SelectIndexOfColumn(4));
            KeyAssignCore.RegisterOperation("Select6thColumn", () => SelectIndexOfColumn(5));
            KeyAssignCore.RegisterOperation("Select7thColumn", () => SelectIndexOfColumn(6));
            KeyAssignCore.RegisterOperation("Select8thColumn", () => SelectIndexOfColumn(7));
            KeyAssignCore.RegisterOperation("Select9thColumn", () => SelectIndexOfColumn(8));
            KeyAssignCore.RegisterOperation("Select10thColumn", () => SelectIndexOfColumn(9));
            KeyAssignCore.RegisterOperation("SelectNthColumn", s => SelectIndexOfColumn(Int32.Parse(s)));

            KeyAssignCore.RegisterOperation("Select1stTweet", () => SelectIndexOfTimeline(0));
            KeyAssignCore.RegisterOperation("Select2ndTweet", () => SelectIndexOfTimeline(1));
            KeyAssignCore.RegisterOperation("Select3rdTweet", () => SelectIndexOfTimeline(2));
            KeyAssignCore.RegisterOperation("Select4thTweet", () => SelectIndexOfTimeline(3));
            KeyAssignCore.RegisterOperation("Select5thTweet", () => SelectIndexOfTimeline(4));
            KeyAssignCore.RegisterOperation("Select6thTweet", () => SelectIndexOfTimeline(5));
            KeyAssignCore.RegisterOperation("Select7thTweet", () => SelectIndexOfTimeline(6));
            KeyAssignCore.RegisterOperation("Select8thTweet", () => SelectIndexOfTimeline(7));
            KeyAssignCore.RegisterOperation("Select9thTweet", () => SelectIndexOfTimeline(8));
            KeyAssignCore.RegisterOperation("Select10thTweet", () => SelectIndexOfTimeline(9));
            KeyAssignCore.RegisterOperation("SelectNthTweet", s => SelectIndexOfTimeline(Int32.Parse(s)));

            KeyAssignCore.RegisterOperation("Open1stUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(0)));
            KeyAssignCore.RegisterOperation("Open2ndUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(1)));
            KeyAssignCore.RegisterOperation("Open3rdUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(2)));
            KeyAssignCore.RegisterOperation("Open4thUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(3)));
            KeyAssignCore.RegisterOperation("Open5thUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(4)));
            KeyAssignCore.RegisterOperation("Open6thUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(5)));
            KeyAssignCore.RegisterOperation("Open7thUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(6)));
            KeyAssignCore.RegisterOperation("Open8thUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(7)));
            KeyAssignCore.RegisterOperation("Open9thUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(8)));
            KeyAssignCore.RegisterOperation("Open10thUrl", () => ExecTVMAction(t => t.OpenIndexOfUrl(9)));
            KeyAssignCore.RegisterOperation("OpenNthUrl", s => ExecTVMAction(t => t.OpenIndexOfUrl(Int32.Parse(s))));

            KeyAssignCore.RegisterOperation("Open1stAction", () => ExecTVMAction(t => t.OpenIndexOfAction(0)));
            KeyAssignCore.RegisterOperation("Open2ndAction", () => ExecTVMAction(t => t.OpenIndexOfAction(1)));
            KeyAssignCore.RegisterOperation("Open3rdAction", () => ExecTVMAction(t => t.OpenIndexOfAction(2)));
            KeyAssignCore.RegisterOperation("Open4thAction", () => ExecTVMAction(t => t.OpenIndexOfAction(3)));
            KeyAssignCore.RegisterOperation("Open5thAction", () => ExecTVMAction(t => t.OpenIndexOfAction(4)));
            KeyAssignCore.RegisterOperation("Open6thAction", () => ExecTVMAction(t => t.OpenIndexOfAction(5)));
            KeyAssignCore.RegisterOperation("Open7thAction", () => ExecTVMAction(t => t.OpenIndexOfAction(6)));
            KeyAssignCore.RegisterOperation("Open8thAction", () => ExecTVMAction(t => t.OpenIndexOfAction(7)));
            KeyAssignCore.RegisterOperation("Open9thAction", () => ExecTVMAction(t => t.OpenIndexOfAction(8)));
            KeyAssignCore.RegisterOperation("Open10thAction", () => ExecTVMAction(t => t.OpenIndexOfAction(9)));
            KeyAssignCore.RegisterOperation("OpenNthAction", s => ExecTVMAction(t => t.OpenIndexOfAction(Int32.Parse(s))));

            // Timeline action
            KeyAssignCore.RegisterOperation("Mention", () => ExecTVMAction(vm =>
                MouseAssignCore.ExecuteAction(vm.Tweet, Configuration.Settings.ReplyMouseActionCandidates.Reply, null)));
            KeyAssignCore.RegisterOperation("MentionSpecific", acc => ExecTVMAction(vm =>
                MouseAssignCore.ExecuteAction(vm.Tweet, Configuration.Settings.ReplyMouseActionCandidates.ReplyFromSpecificAccount, acc)));
            KeyAssignCore.RegisterOperation("MentionImmediately", text => ExecTVMAction(vm =>
                MouseAssignCore.ExecuteAction(vm.Tweet, Configuration.Settings.ReplyMouseActionCandidates.ReplyImmediately, text)));
            KeyAssignCore.RegisterOperation("MentionMulti", () => ExecTVMAction(vm =>
            {
                MouseAssignCore.ExecuteAction(vm.Tweet, Configuration.Settings.ReplyMouseActionCandidates.Reply, null);
                this.SetFocus();
            }));
            KeyAssignCore.RegisterOperation("SendDirectMessage", () => ExecTVMAction(vm => vm.DirectMessageCommand.Execute()));
            KeyAssignCore.RegisterOperation("FavoriteThisTabAll", () => ExecTabAction(vm => vm.FavoriteThisTabAll()));
            KeyAssignCore.RegisterOperation("RetweetThisTabAll", () => ExecTabAction(vm => vm.RetweetThisTabAll()));

            KeyAssignCore.RegisterOperation("Favorite", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavToggle, null)));
            KeyAssignCore.RegisterOperation("FavoriteAdd", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavAdd, null)));
            KeyAssignCore.RegisterOperation("FavoriteRemove", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavRemove, null)));
            KeyAssignCore.RegisterOperation("FavoriteSelect", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavSelect, null)));
            KeyAssignCore.RegisterOperation("FavoriteAddAll", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavAddWithAllAccount, null)));
            KeyAssignCore.RegisterOperation("FavoriteRemoveAll", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavRemoveWithAllAccount, null)));
            KeyAssignCore.RegisterOperation("FavoriteSpecific", acc => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavToggleWithSpecificAccount, acc)));
            KeyAssignCore.RegisterOperation("FavoriteAddSpecific", acc => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavAddWithSpecificAccount, acc)));
            KeyAssignCore.RegisterOperation("FavoriteRemoveSpecific", acc => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.FavMouseActionCandidates.FavRemoveWithSpecificAccount, acc)));
            KeyAssignCore.RegisterOperation("FavoriteAndRetweet", () => ExecTVMAction(vm => vm.ToggleFavoriteAndRetweet()));
            KeyAssignCore.RegisterOperation("FavoriteAndSteal", () => ExecTVMAction(vm => vm.FavoriteAndSteal()));

            KeyAssignCore.RegisterOperation("Retweet", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetToggle, null)));
            KeyAssignCore.RegisterOperation("RetweetCreate", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetCreate, null)));
            KeyAssignCore.RegisterOperation("RetweetDelete", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetDelete, null)));
            KeyAssignCore.RegisterOperation("RetweetSelect", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetSelect, null)));
            KeyAssignCore.RegisterOperation("RetweetCreateAll", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetCreateWithAllAccount, null)));
            KeyAssignCore.RegisterOperation("RetweetDeleteAll", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetDeleteWithAllAccount, null)));
            KeyAssignCore.RegisterOperation("RetweetSpecific", acc => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetToggleWithSpecificAccount, acc)));
            KeyAssignCore.RegisterOperation("RetweetCreateSpecific", acc => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetCreateWithSpecificAccount, acc)));
            KeyAssignCore.RegisterOperation("RetweetDeleteSpecific", acc => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.RetweetMouseActionCandidates.RetweetDeleteWithSpecificAccount, acc)));

            KeyAssignCore.RegisterOperation("UnofficialRetweet", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                 Configuration.Settings.UnofficialRetweetQuoteMouseActionCandidates.DefaultUnofficialRetweet, null)));
            KeyAssignCore.RegisterOperation("QuoteTweet", () => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.UnofficialRetweetQuoteMouseActionCandidates.DefaultQuoteTweet, null)));
            KeyAssignCore.RegisterOperation("CustomUnofficialRetweet", arg => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                 Configuration.Settings.UnofficialRetweetQuoteMouseActionCandidates.CustomUnofficialRetweet, arg)));
            KeyAssignCore.RegisterOperation("CustomQuoteTweet", arg => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.UnofficialRetweetQuoteMouseActionCandidates.CustomQuoteTweet, arg)));
            KeyAssignCore.RegisterOperation("CustomUnofficialRetweetImmediately", arg => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                 Configuration.Settings.UnofficialRetweetQuoteMouseActionCandidates.CustomUnofficialRetweetImmediately, arg)));
            KeyAssignCore.RegisterOperation("CustomQuoteTweetImmediately", arg => ExecTVMAction(vm => MouseAssignCore.ExecuteAction(vm.Tweet,
                Configuration.Settings.UnofficialRetweetQuoteMouseActionCandidates.CustomQuoteTweetImmediately, arg)));

            KeyAssignCore.RegisterOperation("Steal", () => ExecTVMAction(vm => vm.Steal()));

            KeyAssignCore.RegisterOperation("Delete", () => ExecTVMAction(vm => vm.DeleteCommand.Execute()));
            KeyAssignCore.RegisterOperation("Mute", () => ExecTVMAction(vm => vm.MuteCommand.Execute()));
            KeyAssignCore.RegisterOperation("ReportForSpam", () => ExecTVMAction(vm => vm.ReportForSpamCommand.Execute()));
            KeyAssignCore.RegisterOperation("ShowConversation", () => ExecTVMAction(vm => vm.OpenConversationCommand.Execute()));
            KeyAssignCore.RegisterOperation("CreateSelectedUserTab", () => ExecTVMAction(vm => CreateUserTab(vm, false)));
            KeyAssignCore.RegisterOperation("CreateSelectedUserColumn", () => ExecTVMAction(vm => CreateUserTab(vm, true)));
            KeyAssignCore.RegisterOperation("OpenTweetWeb", () => ExecTVMAction(vm => vm.Tweet.ShowTweetCommand.Execute()));
            KeyAssignCore.RegisterOperation("ShowUserDetail", () => ExecTVMAction(vm => vm.ShowUserDetailCommand.Execute()));
            KeyAssignCore.RegisterOperation("CopySTOT", () => ExecTVMAction(vm => vm.Tweet.CopySTOTCommand.Execute()));
            KeyAssignCore.RegisterOperation("CopyWebUrl", () => ExecTVMAction(vm => vm.Tweet.CopyWebUrlCommand.Execute()));
            KeyAssignCore.RegisterOperation("CopyScreenName", () => ExecTVMAction(vm => vm.Tweet.CopyScreenNameCommand.Execute()));

            // Tab Action
            KeyAssignCore.RegisterOperation("Search", () => ExecTabAction(vm =>
            {
                vm.AddTopTimeline(new[] { new FilterCluster() });
                vm.Messenger.Raise(new InteractionMessage("FocusToSearch"));
            }));
            KeyAssignCore.RegisterOperation("RemoveViewStackTop", () => ExecTabAction(vm => vm.RemoveTopTimeline(false)));
            KeyAssignCore.RegisterOperation("RemoveViewStackAll", () => ExecTabAction(vm => vm.RemoveTopTimeline(true)));
            KeyAssignCore.RegisterOperation("OpenUserViewFromSearchBar", () => ExecTabAction(vm => vm.OpenUser()));
            KeyAssignCore.RegisterOperation("CreateTab", () => ExecTabAction(vm => vm.Parent.AddNewTabCommand.Execute()));
            KeyAssignCore.RegisterOperation("CloseTab", () => ExecTabAction(vm => vm.Parent.CloseTab(vm)));
        }

        private void MoveHorizontal(bool directionRight, bool moveBetweenColumn)
        {
            var cc = this.CurrentFocusColumn;
            if (cc == null) return;
            int idxofc = this.Columns.IndexOf(cc);
            int idx = cc.TabItems.IndexOf(cc.SelectedTabViewModel);
            if (moveBetweenColumn)
            {
                if (directionRight)
                {
                    this.CurrentFocusColumn = this.Columns[(idxofc + 1) % this.Columns.Count];
                }
                else
                {
                    this.CurrentFocusColumn = this.Columns[(idxofc + this.Columns.Count - 1) % this.Columns.Count];
                }
            }
            else if (directionRight)
            {
                idx++;
                // →
                if (idx >= cc.TabItems.Count)
                {
                    // move column
                    idx = 0;
                    idxofc++;
                    idxofc %= this.Columns.Count;
                    this.CurrentFocusColumn = this.Columns[idxofc];
                    this.CurrentFocusColumn.SelectedTabViewModel = this.CurrentFocusColumn.TabItems[0];
                }
                else
                {
                    cc.SelectedTabViewModel = cc.TabItems[idx];
                }
            }
            else
            {
                // ←
                idx--;
                if (idx < 0)
                {
                    idx = 0;
                    idxofc--;
                    idxofc += this.Columns.Count;
                    idxofc %= this.Columns.Count;
                    this.CurrentFocusColumn = this.Columns[idxofc];
                    this.CurrentFocusColumn.SelectedTabViewModel = this.CurrentFocusColumn.TabItems[this.CurrentFocusColumn.TabItems.Count - 1];
                }
                else
                {
                    cc.SelectedTabViewModel = cc.TabItems[idx];
                }
            }
        }

        private void SelectIndexOfColumn(int column)
        {
            if (column >= this.Columns.Count) return;
            this.CurrentFocusColumn = this.Columns[column];
        }

        private void SelectIndexOfTab(int tab)
        {
            var cc = this.CurrentFocusColumn;
            if (cc == null) return;
            if (tab >= cc.TabItems.Count) return;
            cc.SelectedTabViewModel = cc.TabItems[tab];
        }

        private void SelectIndexOfTimeline(int tweet)
        {
            var tb = this.CurrentTab;
            if (tb == null) return;
            tb.CurrentForegroundTimeline.CoreViewModel.SetSelect(tb.CurrentForegroundTimeline.CoreViewModel.ScrollIndex + tweet);
        }

        private void MoveVertical(ListSelectionKind selectKind)
        {
            var cc = this.CurrentTab == null ? null : this.CurrentTab.CurrentForegroundTimeline;
            if (selectKind == ListSelectionKind.SelectAbove && Setting.Instance.TimelineExperienceProperty.MoveAboveTopToDeselect)
                selectKind = ListSelectionKind.SelectAboveAndNull;
            if (cc != null)
                cc.SetSelect(selectKind);
        }

        private void ExecTabAction(Action<TabViewModel> action)
        {
            KeyAssignHelper.ExecuteTabAction(action);
        }

        private void ExecTVMAction(Action<TabDependentTweetViewModel> action)
        {
            KeyAssignHelper.ExecuteTVMAction(action);
        }

        private void CreateUserTab(TabDependentTweetViewModel tvm, bool newColumn)
        {
            var filter = new[] { new FilterUserId(tvm.Tweet.Status.User.NumericId) };
            var desc = "@" + tvm.Tweet.Status.User.ScreenName;
            if (newColumn)
            {
                var column = tvm.Parent.Parent.Parent.CreateColumn();
                column.AddTab(new Configuration.Tabs.TabProperty() { Name = desc, TweetSources = filter });
            }
            else
            {
                tvm.Parent.Parent.AddTab(new Configuration.Tabs.TabProperty() { Name = desc, TweetSources = filter });
            }
        }

        #endregion
    }

    public enum ColumnsLocation
    {
        Next,
        Previous
    }
}
