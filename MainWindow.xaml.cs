using NAudio.Wave;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.IO;
using System.Collections.ObjectModel;
using NAudio.Gui;
using System.Windows.Media;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using System.Globalization;
using System.Windows.Data;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace Blueberry
{

    public class Track
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public TimeSpan Duration { get; set; }

        public string Kbps { get; set; }
    }

    public class Playlist
    {
        public string Name { get; set; }
        public ObservableCollection<Track> Tracks { get; set; } = new ObservableCollection<Track>();
        public string CoverImagePath { get; set; } // Путь к обложке плейлиста
    }


    public partial class MainWindow : Window
    {
        private WaveOutEvent waveOutDevice;
        public int currentIndex;
        public Playlist currentPlaylist;
        private readonly object lockObject = new object();
        private ObservableCollection<Playlist> playlists = new ObservableCollection<Playlist>();
        private TracksWindow tracksWindow;
        private Dictionary<Playlist, PlaylistEditorWindow> openEditorWindows = new Dictionary<Playlist, PlaylistEditorWindow>();
        private float previousVolume = 1.0f;
        public MainWindow()
        {
            InitializeComponent();
            waveOutDevice = new WaveOutEvent();
            waveOutDevice.PlaybackStopped += (sender, e) => {
                Dispatcher.Invoke(() => HandlePlaybackStopped());
            };

            Task.Delay(500).ContinueWith(t => {
                waveOutDevice = new WaveOutEvent();
                waveOutDevice.PlaybackStopped += (sender, e) => {
                    Dispatcher.Invoke(() => HandlePlaybackStopped());
                };
            });
            currentIndex = 0;
            currentPlaylist = new Playlist();
            StartTimer();
            LoadPlaylists();
            isRepeating = false;
            autoNextTrack = true;  // Предотвратить автоматическое переключение после остановки
            EnsureAudioFileReaderInitialized();
            
            UpdatePlaybackState(true);
        }
        private void HandlePlaybackStopped()
        {
            if (waveOutDevice.PlaybackState == PlaybackState.Stopped) // Проверяем, что воспроизведение действительно завершилось
            {
                if (isRepeating)
                {
                    PrepareForNewTrack();
                    PlayTrack(currentPlaylist.Tracks[currentIndex]);
                }
                else if (autoNextTrack)
                {
                    // Логика для автоматического перехода к следующему треку
                    currentIndex = (currentIndex + 1) % currentPlaylist.Tracks.Count;
                    PrepareForNewTrack();
                    PlayTrack(currentPlaylist.Tracks[currentIndex]);
                }
                autoNextTrack = true; // Переустанавливаем флаг для следующего трека
            }
        }
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            lock (lockObject)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog { Multiselect = false, Filter = "MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*" };
                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedFile = Path.GetFullPath(openFileDialog.FileName);
                    // Assuming you want to play the selected file without changing the playlist
                    PlayTrack(new Track { FilePath = selectedFile, FileName = Path.GetFileName(selectedFile) });
                    LoadTrackMetadata(selectedFile);
                    UpdatePlaybackState(true);
                }
            }
        }
        private void OpenFiles_Click(object sender, RoutedEventArgs e)
        {
            lock (lockObject)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog { Multiselect = true, Filter = "MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*" };
                if (openFileDialog.ShowDialog() == true)
                {
                    // Load all selected files directly for playback
                    List<Track> tracksToPlay = new List<Track>();
                    foreach (string file in openFileDialog.FileNames)
                    {
                        tracksToPlay.Add(new Track { FilePath = file, FileName = Path.GetFileName(file) });
                    }

                    // If you want to start playing the first track immediately
                    if (tracksToPlay.Count > 0)
                    {
                        // Assuming currentIndex and currentPlaylist are what you normally use to manage and play tracks
                        currentIndex = 0; // Start playing from the first file in the new list
                        currentPlaylist = new Playlist(); // Temporarily replace the currentPlaylist
                        currentPlaylist.Tracks = new ObservableCollection<Track>(tracksToPlay);

                        PlayTrack(currentPlaylist.Tracks[currentIndex]); // Play the first track
                        UpdatePlaybackState(true); // Update the playback state as needed
                    }
                }
            }
        }
        private void OpenFilesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFiles_Click(sender, e);
        }
        private void LoadTrackMetadata(string path)
        {
            try
            {
                var file = TagLib.File.Create(path);

                // Заполнение данных о треке
                var trackTitle = file.Tag.Title;  // Получаем название трека из тегов
                fileNameLabel.Content = string.IsNullOrEmpty(trackTitle) ? Path.GetFileName(path) : trackTitle;  // Используем название файла, если название трека не задано

                albumLabel.Content = "Album: " + (string.IsNullOrEmpty(file.Tag.Album) ? "Unknown" : file.Tag.Album); // Название альбома
                artistLabel.Content = "Artist: " + (file.Tag.Performers.Length > 0 ? file.Tag.Performers[0] : "Unknown"); // Исполнитель
                var kbps = (file.Properties.AudioBitrate > 0) ? file.Properties.AudioBitrate.ToString() : "Unknown"; // Битрейт
                kbpsLabel.Content = "kbps: " + kbps;

                // Обработка изображения обложки
                var image = file.Tag.Pictures.FirstOrDefault();
                if (image != null)
                {
                    var imageStream = new MemoryStream(image.Data.Data);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = imageStream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Обеспечение доступности изображения в UI потоке
                    albumArt.Source = bitmap; // Установка изображения в элемент Image
                }
                else
                {
                    // Загрузка изображения по умолчанию, если обложка альбома отсутствует
                    albumArt.Source = new BitmapImage(new Uri("pack://application:,,,/AssemblyName;component/res/default.png")); // Укажите путь к вашему изображению по умолчанию
                }
            }
            catch (Exception ex)
            {
                var file = TagLib.File.Create(path);
                Debug.WriteLine("Ошибка при чтении метаданных файла: " + ex.Message);
                // В случае ошибки устанавливаем значения по умолчанию
                fileNameLabel.Content = Path.GetFileName(path); 
                albumLabel.Content = "Album: Unknown";
                artistLabel.Content = "Artist: Unknown";
                var kbps = (file.Properties.AudioBitrate > 0) ? file.Properties.AudioBitrate.ToString() : "Unknown"; // Битрейт
                kbpsLabel.Content = "kbps: " + kbps;
                albumArt.Source = new BitmapImage(new Uri("pack://application:,,,/Blueberry;component/res/default.png")); ; // Укажите путь к вашему изображению по умолчанию
            }
        }
        public static int GetKbpsFromMetadata(string filePath)
        {
            try
            {
                var file = TagLib.File.Create(filePath);
                // Проверяем, есть ли доступные аудио свойства
                if (file.Properties != null)
                {
                    int bitrate = file.Properties.AudioBitrate;  // Битрейт в кбит/с
                    return bitrate;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка при чтении метаданных файла: " + ex.Message);
            }
            return -1;  // Возвращает -1 в случае ошибки или если битрейт не доступен
        }
        private void CreatePlaylist_Click(object sender, RoutedEventArgs e)
        {
            CreatePlaylistWindow createPlaylistWindow = new CreatePlaylistWindow();
            if (createPlaylistWindow.ShowDialog() == true)
            {
                Playlist newPlaylist = createPlaylistWindow.NewPlaylist;
                playlists.Add(newPlaylist);
                DisplayPlaylist(newPlaylist);
            }
        }
        
        private void DeletePlaylist(Playlist playlist, Grid grid)
        {
            playlists.Remove(playlist);
            RefreshPlaylistPanel(); // Обновление после удаления
        }
        private void PrepareForNewTrack()
        {
            lock (lockObject)
            {
                if (waveOutDevice != null && waveOutDevice.PlaybackState != PlaybackState.Stopped)
                {
                    waveOutDevice.Stop();
                }

                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                    audioFileReader = null;
                }
            }
        }
        public void UpdatePlaybackState(bool isPlaying)
        {
            IsPlaying = isPlaying;
            playPauseButton.Content = IsPlaying ? "⏸️" : "▶";
        }
        private bool autoNextTrack = true;
        private void NextTrack_Click(object sender, RoutedEventArgs e)
        {
            lock (lockObject)
            {
                if (currentPlaylist.Tracks.Count > 0)  // Проверяем, что в плейлисте есть треки
                {   
                    if (isRepeating)
                    {
                        isRepeating = true;
                        autoNextTrack = false;  // Предотвратить автоматическое переключение после остановки
                    }
                    else if(!isRepeating)
                    {
                        isRepeating = false;
                        autoNextTrack = true;
                    }
                    EnsureAudioFileReaderInitialized();
                    currentIndex = (currentIndex + 1) % currentPlaylist.Tracks.Count;
                    PrepareForNewTrack();
                    PlayTrack(currentPlaylist.Tracks[currentIndex]);
                    UpdatePlaybackState(true);
                }
                else
                {
                    MessageBox.Show("No tracks in playlist to advance to.");
                }
            }
        }
        private void PreviousTrack_Click(object sender, RoutedEventArgs e)
        {
            lock (lockObject)
            {
                if (currentPlaylist.Tracks.Count > 0)  // Проверяем, что в плейлисте есть треки
                {
                    if (isRepeating)
                    {
                        isRepeating = true;
                        autoNextTrack = false;  // Предотвратить автоматическое переключение после остановки
                    }
                    else if (!isRepeating)
                    {
                        isRepeating = false;
                        autoNextTrack = true;
                    }  // Предотвратить автоматическое переключение после остановки
                    EnsureAudioFileReaderInitialized();
                    if (audioFileReader.CurrentTime.TotalSeconds > 5)
                    {
                        PrepareForNewTrack();
                        PlayTrack(currentPlaylist.Tracks[currentIndex]);
                    }
                    else
                    {
                        currentIndex = (currentIndex - 1 < 0) ? currentPlaylist.Tracks.Count - 1 : currentIndex - 1;
                        PrepareForNewTrack();
                        PlayTrack(currentPlaylist.Tracks[currentIndex]);
                    }
                    UpdatePlaybackState(true);
                }
                else
                {
                    MessageBox.Show("No tracks in playlist to go back to.");
                }
            }
        }
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (waveOutDevice != null && waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                waveOutDevice.Volume = (float)(e.NewValue);
                muteButton.Content = waveOutDevice.Volume == 0 ? "🔇" : "🔊";
            }
            
        }
        private void TrackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e.NewValue != audioFileReader.CurrentTime.TotalSeconds)
            {
                audioFileReader.CurrentTime = TimeSpan.FromSeconds(e.NewValue);
            }
        }
        private void UpdateTrackTime()
        {
            if (waveOutDevice.PlaybackState == PlaybackState.Playing)
            {
                double currentTime = audioFileReader.CurrentTime.TotalSeconds;
                double totalTime = audioFileReader.TotalTime.TotalSeconds;

                current_time.Text = TimeSpan.FromSeconds(currentTime).ToString(@"mm\:ss");
                all_time.Text = TimeSpan.FromSeconds(totalTime).ToString(@"mm\:ss");

                trackSlider.Maximum = totalTime;
                if (!trackSlider.IsMouseCaptureWithin)  // Проверка, чтобы не обновлять слайдер, если пользователь в данный момент его тащит
                {
                    trackSlider.Value = currentTime;
                }
            }
        }
        private void StartTimer()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (sender, e) => UpdateTrackTime();
            timer.Start();
        }
        private bool IsPlaying = true;  
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (all_time.Text!="00:00")
            {
                if (IsPlaying)
                {
                    waveOutDevice.Pause();
                    playPauseButton.Content = "▶";  // Изменяем иконку кнопки на 'Play'
                }
                else
                {
                    waveOutDevice.Play();
                    playPauseButton.Content = "⏸️";  // Изменяем иконку кнопки на 'Pause'
                }
                IsPlaying = !IsPlaying;  // Переключаем состояние воспроизведения
            }
        }
        private bool isRepeating = false;
        private void ToggleRepeat_Click(object sender, RoutedEventArgs e)
        {
            isRepeating = !isRepeating;  // Переключаем режим повтора

            // Обновление иконки на кнопке в зависимости от состояния isRepeating
            if (isRepeating)
            {
                repeatButton.Content = "🔂";  // Иконка для активного режима повтора
            }
            else
            {
                repeatButton.Content = "🔁";  // Иконка для отключенного режима повтора
            }
        }
        private void ShowEditButtons(Grid grid, Playlist playlist)
        {
            Debug.WriteLine("Attempting to show buttons for: " + playlist.Name);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(5, 0, 5, 0),
                Opacity = 0.5 // Делаем панель кнопок полупрозрачной
            };

            Button editButton = new Button { Content = "🖊️", Margin = new Thickness(0), Visibility = Visibility.Visible, Width = 30, Height = 30 };
            Button deleteButton = new Button { Content = "🗑️", Margin = new Thickness(0), Visibility = Visibility.Visible, Width = 30, Height = 30 };
            Button playButton = new Button { Content = "▶", Margin = new Thickness(0), Visibility = Visibility.Visible, Width = 30, Height = 30 };
            Button showTracksButton = new Button { Content = "🎶", Margin = new Thickness(0), Visibility = Visibility.Visible, Width = 30, Height = 30 };
            playButton.Click += (s, e) => PlaylistButton_Click(playlist);
            editButton.Click += (s, e) => EditPlaylist_Click(s, e, playlist);
            deleteButton.Click += (s, e) => DeletePlaylist(playlist, grid);
            showTracksButton.Click += (s, e) => ShowTracksWindow(playlist);

            buttonPanel.Children.Add(editButton);
            buttonPanel.Children.Add(playButton);
            buttonPanel.Children.Add(showTracksButton);
            buttonPanel.Children.Add(deleteButton);

            if (!grid.Children.Contains(buttonPanel))
            {
                grid.Children.Add(buttonPanel);
            }
        }
        private void EditPlaylist_Click(object sender, RoutedEventArgs e, Playlist playlist)
        {
            // Проверяем, открыто ли уже окно редактирования для данного плейлиста
            if (openEditorWindows.TryGetValue(playlist, out var editWindow) && editWindow.IsLoaded)
            {
                editWindow.Activate();  // Активация окна, если оно уже открыто
            }
            else
            {
                // Создание нового окна, если оно не открыто
                editWindow = new PlaylistEditorWindow(playlist);
                editWindow.Show();
                editWindow.Closed += (s, args) => openEditorWindows.Remove(playlist);  // Удаление из словаря при закрытии окна
                openEditorWindows[playlist] = editWindow;  // Добавление окна в словарь
            }
        }

        private void ShowTracksWindow(Playlist playlist)
        {
            if (tracksWindow == null || !tracksWindow.IsLoaded)  // Проверяем, существует ли окно и загружено ли оно
            {
                tracksWindow = new TracksWindow(playlist);  // Создание нового окна, если необходимо
                tracksWindow.Closed += (s, e) => tracksWindow = null;  // Обнуляем ссылку при закрытии окна
                tracksWindow.Show();
            }
            else
            {
                tracksWindow.Activate();  // Активация окна, если оно уже открыто
            }
        }
        private void HideEditButtons(Grid grid)
        {
            // Получаем все элементы StackPanel, содержащие кнопки управления
            var buttonPanels = grid.Children.OfType<StackPanel>().Where(panel => panel.Children.OfType<Button>().Any()).ToList();

            // Удаляем их и делаем полупрозрачными
            foreach (var buttonPanel in buttonPanels)
            {
                grid.Children.Remove(buttonPanel);
            }
        }
        public void RefreshPlaylistPanel()
        {
            playlistPanel.Children.Clear(); // Очистка старых виджетов
            foreach (var playlist in playlists)
            {
                DisplayPlaylist(playlist); // Переотображение всех плейлистов
            }
        }
        private void DisplayPlaylist(Playlist playlist)
        {
            // Создаем контейнер для элементов плейлиста
            Border border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(5)
            };

            Grid grid = new Grid { Margin = new Thickness(0) };
            grid.MouseEnter += (s, e) => ShowEditButtons(grid, playlist);
            grid.MouseLeave += (s, e) => HideEditButtons(grid);

            // Изображение обложки
            Image image = new Image
            {
                Height = 200,
                Opacity = 0.8
            };


            try
            {
                image.Source = new BitmapImage(new Uri(playlist.CoverImagePath, UriKind.RelativeOrAbsolute));
            }
            catch (Exception ex)
            {
                // Здесь логируем ошибку, если требуется
                Console.WriteLine("Не удалось загрузить изображение обложки: " + ex.Message);
                // Установка изображения по умолчанию, если загрузка не удалась
                image.Source = new BitmapImage(new Uri("pack://application:,,,/Blueberry;component/res/default.png"));
            }

            // Панель для текстовой информации
            StackPanel stackPanel = new StackPanel
            {
                Background = Brushes.Transparent,
                Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            // Имя плейлиста
            Label nameLabel = new Label
            {
                Content = playlist.Name,
                FontSize = 48,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("BEBAS NEUE"),
                Opacity = 0.9
            };

            // Информация о количестве треков и длительности
            StackPanel otherInfoPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(5, 5, 5, 5)
            };

            Label tracksLabel = new Label
            {
                Content = $"{playlist.Tracks.Count} tracks",
                FontSize = 24,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Proletarsk"),
                Opacity = 0.9
            };
            // Сборка элементов интерфейса
            otherInfoPanel.Children.Add(nameLabel);
            otherInfoPanel.Children.Add(tracksLabel);
            stackPanel.Children.Add(otherInfoPanel);
            grid.Children.Add(image);
            grid.Children.Add(stackPanel);
            border.Child = grid;

            playlistPanel.Children.Add(border);
        }
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (isRepeating)
            {
                // В режиме повтора воспроизводим текущий трек снова
                PrepareForNewTrack();
                PlayTrack(currentPlaylist.Tracks[currentIndex]);
            }
            else if (autoNextTrack)
            {
                // Переход к следующему треку, если не в режиме повтора
                if (currentIndex + 1 < currentPlaylist.Tracks.Count)
                {
                    currentIndex++;  // Переход к следующему треку
                }
                else
                {
                    currentIndex = 0;  // Начать с начала, если это последний трек
                }
                PrepareForNewTrack();
                PlayTrack(currentPlaylist.Tracks[currentIndex]);
            }
            else
            {
                // Сбросить autoNextTrack после автоматической обработки
                autoNextTrack = true;
            }
        }
        private AudioFileReader audioFileReader;
        public void PlayTrack(Track track)
        {
            lock (lockObject)
            {
                Dispatcher.Invoke(() => {
                    PrepareForNewTrack();  // Подготовка ресурсов в потоке UI

                    try
                    {
                        if (File.Exists(track.FilePath))
                        {
                            audioFileReader = new AudioFileReader(track.FilePath);
                            waveOutDevice.Init(audioFileReader);
                            waveOutDevice.Play();
                            UpdateTrackInfoDisplay(track);
                        }
                        else
                        {
                            MessageBox.Show("Файл не найден: " + track.FilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Ошибка при воспроизведении трека: " + ex.Message);
                        MessageBox.Show("Ошибка при воспроизведении файла: " + track.FilePath);
                    }
                });
            }
        }
        private void UpdateTrackInfoDisplay(Track track)
        {
            // Загрузка метаданных из файла
            var file = TagLib.File.Create(track.FilePath);

            LoadTrackMetadata(track.FilePath);
        }
        private void PlaylistButton_Click(Playlist playlist)
        {
            UpdatePlaybackState(true);
            lock (lockObject)
            {
                if (playlist != null && playlist.Tracks.Count > 0)
                {
                    currentPlaylist = playlist;  // Обновляем текущий плейлист
                    currentIndex = 0;           // Начинаем с первого трека

                    PlayTrack(playlist.Tracks[currentIndex]);

                }
                else
                {
                    MessageBox.Show("This playlist is empty.");
                }
            }
        }
        private void SavePlaylists()
        {
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "playlists.json");
            string json = JsonConvert.SerializeObject(playlists);
            File.WriteAllText(filePath, json);
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SavePlaylists();
        }
        private void LoadPlaylists()
        {
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "playlists.json");
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var loadedPlaylists = JsonConvert.DeserializeObject<ObservableCollection<Playlist>>(json);
                if (loadedPlaylists != null)
                {
                    playlists = loadedPlaylists;
                    foreach (var playlist in playlists)
                    {
                        DisplayPlaylist(playlist);
                    }
                }
            }
        }
        private void EnsureAudioFileReaderInitialized()
        {
            lock (lockObject)
            {
                if (audioFileReader == null || audioFileReader.Length == 0)
                {
                    if (currentPlaylist.Tracks.Any())
                    {
                        var track = currentPlaylist.Tracks[currentIndex];
                        try
                        {
                            audioFileReader = new AudioFileReader(track.FilePath);
                            if (waveOutDevice.PlaybackState != PlaybackState.Stopped)
                            {
                                waveOutDevice.Stop();
                            }
                            waveOutDevice.Init(audioFileReader);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Ошибка при инициализации аудио файла: " + ex.Message);
                            MessageBox.Show("Не удалось открыть файл: " + track.FilePath);
                        }
                    }
                }
            }
        }
        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (waveOutDevice.Volume > 0)
            {
                // Запоминаем текущую громкость и устанавливаем звук на 0
                previousVolume = waveOutDevice.Volume;
                waveOutDevice.Volume = 0;
                muteButton.Content = "🔇";  // Меняем иконку на "выключенный звук"
                volumeSlider.Value = 0;  // Устанавливаем слайдер в положение 0
            }
            else
            {
                // Возвращаем звук на предыдущий уровень
                waveOutDevice.Volume = previousVolume > 0 ? previousVolume : 0.5f; // Если предыдущее значение было 0, устанавливаем на половину
                muteButton.Content = "🔊";  // Меняем иконку на "включенный звук"
                volumeSlider.Value = waveOutDevice.Volume;  // Синхронизируем слайдер с уровнем звука
            }
        }
    }
}
