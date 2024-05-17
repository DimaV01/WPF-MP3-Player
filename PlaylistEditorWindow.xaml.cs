using NAudio.Wave;
using Microsoft.Win32;
using System.Windows;
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

namespace Blueberry
{

    public partial class PlaylistEditorWindow : Window
    {
        public Playlist CurrentPlaylist { get; set; }
        private Playlist tempPlaylist;

        public PlaylistEditorWindow(Playlist playlist)
        {
            InitializeComponent();
            if (playlist == null) throw new ArgumentNullException(nameof(playlist));
            CurrentPlaylist = playlist;  // Убедитесь, что это значение присвоено
            tempPlaylist = CreateDeepCopyOfPlaylist(playlist);  // Предполагается, что вы используете метод для создания полной копии
            this.DataContext = tempPlaylist;

            // Загрузка изображения обложки с обработкой исключений
            try
            {
                playlistCover.Source = new BitmapImage(new Uri(tempPlaylist.CoverImagePath, UriKind.RelativeOrAbsolute));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка загрузки изображения обложки: " + ex.Message);
                // Установка изображения по умолчанию, если загрузка не удалась
                playlistCover.Source = new BitmapImage(new Uri("pack://application:,,,/Blueberry;component/res/default.png"));
            }

            trackListView.ItemsSource = tempPlaylist.Tracks;
        }


        private Playlist CreateDeepCopyOfPlaylist(Playlist original)
        {
            var copiedPlaylist = new Playlist
            {
                Name = original.Name,
                CoverImagePath = original.CoverImagePath,
                Tracks = new ObservableCollection<Track>()
            };

            foreach (Track originalTrack in original.Tracks)
            {
                Track copiedTrack = new Track
                {
                    FileName = originalTrack.FileName,
                    FilePath = originalTrack.FilePath,
                    // If there are other properties like Duration, you should copy them here as well
                    Duration = originalTrack.Duration // Assuming this is how you'd handle other properties
                };
                copiedPlaylist.Tracks.Add(copiedTrack);
            }

            tempPlaylist = copiedPlaylist;  // Set the temporary playlist
            return copiedPlaylist;
        }
    
        private void DeleteTrack_Click(object sender, RoutedEventArgs e)
        {
            var track = (sender as Button)?.CommandParameter as Track;
            if (track != null)
            {
                tempPlaylist.Tracks.Remove(track);
                trackListView.Items.Refresh(); // Refresh the list view to show updated track list
            }
        }


        private void ChangeCover_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.png)|*.jpg;*.png",
                Title = "Select a cover image"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                playlistCover.Source = new BitmapImage(new Uri(openFileDialog.FileName));
                tempPlaylist.CoverImagePath = openFileDialog.FileName; // Update the cover image path in the playlist
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (tempPlaylist == null)
            {
                MessageBox.Show("Temporary playlist data is missing.");
                return;
            }

            if (CurrentPlaylist == null)
            {
                MessageBox.Show("Current playlist data is missing.");
                return;
            }

            // Proceed with copying data
            CurrentPlaylist.Name = tempPlaylist.Name;
            CurrentPlaylist.CoverImagePath = tempPlaylist.CoverImagePath;
            CurrentPlaylist.Tracks.Clear();
            foreach (Track track in tempPlaylist.Tracks)
            {
                CurrentPlaylist.Tracks.Add(new Track
                {
                    FileName = track.FileName,
                    FilePath = track.FilePath,
                    Duration = track.Duration
                });
            }

            // Assuming you have a way to update the main UI
            MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.RefreshPlaylistPanel(); // Make sure this method exists and is implemented
            }

            this.Close();
        }




        private void AddTrack_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav",
                Multiselect = true,
                Title = "Select Tracks"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    Track newTrack = new Track
                    {
                        FileName = Path.GetFileName(file),  // Make sure to set the Name property correctly
                        FilePath = file
                    };
                    tempPlaylist.Tracks.Add(newTrack);
                }
                trackListView.Items.Refresh();  // Refresh the list view to show new tracks
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Simply close the window without saving
        }


    }
}
