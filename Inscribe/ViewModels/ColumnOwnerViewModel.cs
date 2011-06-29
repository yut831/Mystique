﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Inscribe.Filter;
using Livet;

namespace Inscribe.ViewModels
{
    public class ColumnOwnerViewModel : ViewModel
    {
        public ColumnOwnerViewModel()
        {
            var cc = CreateColumn();
            cc.AddTab(
                new Inscribe.Configuration.Tabs.TabProperty() { Name = "コ", TweetSources = new[] { new FilterCluster() { ConcatenateAnd = true } } });
            cc.AddTab(
                new Inscribe.Configuration.Tabs.TabProperty() { Name = "マ", TweetSources = new[] { new FilterCluster() { ConcatenateAnd = true } } });
            cc.AddTab(
                new Inscribe.Configuration.Tabs.TabProperty() { Name = "ン", TweetSources = new[] { new FilterCluster() { ConcatenateAnd = true } } });
            cc.AddTab(
                new Inscribe.Configuration.Tabs.TabProperty() { Name = "ド", TweetSources = new[] { new FilterCluster() { ConcatenateAnd = true } } });
            cc.AddTab(
                new Inscribe.Configuration.Tabs.TabProperty() { Name = "ー", TweetSources = new[] { new FilterCluster() { ConcatenateAnd = true } } });
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
            nvm.GotFocus += (o, e) => CurrentFocusColumn = nvm;
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
                this._columns.Add(new ColumnViewModel(this));
        }

        private ColumnViewModel _currentFocusColumn = null;
        public ColumnViewModel CurrentFocusColumn
        {
            get
            {
                return this._currentFocusColumn;
            }
            protected set
            {
                this._currentFocusColumn = value;
                RaisePropertyChanged(() => CurrentFocusColumn);
            }
        }

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
    }

    public enum ColumnsLocation
    {
        Next,
        Previous
    }
}