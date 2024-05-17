using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Blueberry
{
    /// <summary>
    /// Логика взаимодействия для TracksWindow.xaml
    /// </summary>
    public partial class TracksWindow : Window
    {
        private Playlist _playlist;

        public TracksWindow(Playlist playlist)
        {
            InitializeComponent();
            _playlist = playlist;
            BuildTracksList();
        }

        private void BuildTracksList()
        {
            foreach (var track in _playlist.Tracks)
            {
                Grid trackGrid = new Grid { Margin = new Thickness(5) };
                trackGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // Колонка для обложки
                trackGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Пространство между обложкой и названием
                trackGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Колонка для текста
                trackGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Колонка для кнопки

                Image coverImage = new Image
                {
                    Width = 50,
                    Height = 50,
                    Margin = new Thickness(5)
                };

                try
                {
                    var file = TagLib.File.Create(track.FilePath);
                    var image = file.Tag.Pictures.FirstOrDefault();
                    if (image != null)
                    {
                        using (var ms = new MemoryStream(image.Data.Data))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            coverImage.Source = bitmap;
                        }
                    }
                    else
                    {
                        coverImage.Source = new BitmapImage(new Uri("pack://application:,,,/Blueberry;component/res/default.png"));
                    }
                }
                catch
                {
                    coverImage.Source = new BitmapImage(new Uri("pack://application:,,,/Blueberry;component/res/default.png"));
                }

                Label trackLabel = new Label
                {
                    Content = track.FileName,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#707070")),
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center
                };

                Button playButton = new Button
                {
                    Content = "▶",
                    Tag = _playlist.Tracks.IndexOf(track),
                    Width = 30,
                    Height = 30,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#b3b3b3")),
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                playButton.Click += TrackButton_Click;

                // Распределение элементов по столбцам
                Grid.SetColumn(coverImage, 0);
                Grid.SetColumn(trackLabel, 2);
                Grid.SetColumn(playButton, 3);

                trackGrid.Children.Add(coverImage);
                trackGrid.Children.Add(trackLabel);
                trackGrid.Children.Add(playButton);

                TracksPanel.Children.Add(trackGrid);
            }
        }


        private void TrackButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int trackIndex = (int)button.Tag;
            PlayTrackFromPlaylist(trackIndex);
        }

        private void PlayTrackFromPlaylist(int trackIndex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // Обновление текущего плейлиста и индекса в MainWindow
                    mainWindow.currentPlaylist = _playlist;  // Установка плейлиста из TracksWindow как текущего в MainWindow
                    mainWindow.currentIndex = trackIndex;  // Обновление текущего индекса трека

                    mainWindow.PlayTrack(_playlist.Tracks[trackIndex]);  // Воспроизведение выбранного трека
                    mainWindow.UpdatePlaybackState(true);
                }
            });
        }

    }

}
