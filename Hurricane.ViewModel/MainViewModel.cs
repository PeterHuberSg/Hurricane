﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Hurricane.Model;
using Hurricane.Model.AudioEngine;
using Hurricane.Model.Music;
using Hurricane.Model.Music.TrackProperties;
using Hurricane.Model.Notifications;
using Hurricane.Model.Settings;
using Hurricane.Utilities;
using Hurricane.ViewModel.MainView;
using Hurricane.ViewModel.MainView.Base;

namespace Hurricane.ViewModel
{
    public class MainViewModel : PropertyChangedBase
    {
        private ISideListItem _selectedViewItem;
        private ViewManager _viewManager;
        private int _currentMainView = 1;
        private readonly ViewController _viewController;
        private IViewItem _specialView;

        private RelayCommand _openSettingsCommand;
        private RelayCommand _playPauseCommand;
        private RelayCommand _cancelProgressNotificationCommand;
        private RelayCommand _forwardCommand;
        private RelayCommand _backCommand;

        public MainViewModel()
        {
            MusicDataManager = new MusicDataManager();
            MusicDataManager.MusicManager.AudioEngine.ErrorOccurred += AudioEngine_ErrorOccurred;
            Application.Current.MainWindow.Closing += MainWindow_Closing;
            NotificationManager = new NotificationManager();
            _viewController = new ViewController(OpenArtist);
        }

        public event EventHandler RefreshView;

        public MusicDataManager MusicDataManager { get; }
        public NotificationManager NotificationManager { get; }
        public SettingsViewModel SettingsViewModel { get; private set; }
        public SettingsData Settings { get; private set; }

        public ViewManager ViewManager
        {
            get { return _viewManager; }
            set { SetProperty(value, ref _viewManager); }
        }

        public ISideListItem SelectedViewItem
        {
            get { return _selectedViewItem; }
            protected set
            {
                if (SetProperty(value, ref _selectedViewItem))
                    value.Load(MusicDataManager, _viewController, NotificationManager).Forget();
            }
        }

        public int CurrentMainView
        {
            get { return _currentMainView; }
            set { SetProperty(value, ref _currentMainView); }
        }

        public IViewItem SpecialView
        {
            get { return _specialView; }
            set
            {
                if (SetProperty(value, ref _specialView))
                    value?.Load(MusicDataManager, _viewController, NotificationManager).Forget();
            }
        }

        public RelayCommand OpenSettingsCommand
        {
            get
            {
                return _openSettingsCommand ?? (_openSettingsCommand = new RelayCommand(parameter =>
                {
                    CurrentMainView = CurrentMainView == 0 ? 1 : 0;
                }));
            }
        }

        public RelayCommand PlayPauseCommand
        {
            get
            {
                return _playPauseCommand ?? (_playPauseCommand = new RelayCommand(parameter =>
                {
                    MusicDataManager.MusicManager.AudioEngine.TogglePlayPause();
                }));
            }
        }

        public RelayCommand CancelProgressNotificationCommand
        {
            get
            {
                return _cancelProgressNotificationCommand ??
                       (_cancelProgressNotificationCommand = new RelayCommand(parameter =>
                       {
                           ((ProgressNotification) parameter).Cancel();
                       }));
            }
        }

        public RelayCommand ForwardCommand
        {
            get { return _forwardCommand ?? (_forwardCommand = new RelayCommand(parameter => { MusicDataManager.MusicManager.GoForward(); })); }
        }

        public RelayCommand BackCommand
        {
            get { return _backCommand ?? (_backCommand = new RelayCommand(parameter => { MusicDataManager.MusicManager.GoBack(); })); }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            MusicDataManager.Save(AppDomain.CurrentDomain.BaseDirectory);
            SettingsManager.Save("settings.xml");
            MusicDataManager.Dispose();
        }

        public async Task LoadData()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var settingsFile = new FileInfo("settings.xml");
                if (settingsFile.Exists)
                    SettingsManager.Load(settingsFile.FullName);
                else SettingsManager.InitalizeNew();
                Settings = SettingsManager.Current;
                
                await MusicDataManager.Load(AppDomain.CurrentDomain.BaseDirectory);
                Debug.Print($"Dataloading time: {sw.ElapsedMilliseconds}");
                SettingsViewModel = new SettingsViewModel(MusicDataManager, () => RefreshView?.Invoke(this, EventArgs.Empty));
            }
            catch (Exception)
            {
                NotificationManager.ShowInformation(Application.Current.Resources["Error"].ToString(),
                    Application.Current.Resources["ErrorWhileLoadingData"].ToString(), MessageNotificationIcon.Error);
            }

            ViewManager = new ViewManager(MusicDataManager.Playlists);
            ViewManager.ViewItems.First(x => x is QueueView)
                .Load(MusicDataManager, _viewController, NotificationManager).Forget(); //Important because the queue view wants to set an event
            SelectedViewItem = ViewManager.ViewItems[0];
        }

        private void AudioEngine_ErrorOccurred(object sender, ErrorOccurredEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                new Action(
                    () =>
                        NotificationManager.ShowInformation(Application.Current.Resources["PlaybackError"].ToString(),
                            e.ErrorMessage, MessageNotificationIcon.Error)));
        }

        private void OpenArtist(Artist artist)
        {
            SpecialView = new ArtistView(artist, () => SpecialView = null);
        }
    }
}