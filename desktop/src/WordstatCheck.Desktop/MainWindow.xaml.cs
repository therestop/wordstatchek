using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WordstatCheck.Core;

namespace WordstatCheck.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> activity = [];
    private CancellationTokenSource? cancellation;

    public MainWindow()
    {
        InitializeComponent();
        ActivityList.ItemsSource = activity;
        ApiKeyBox.Password = Environment.GetEnvironmentVariable("YANDEX_SEARCH_API_KEY") ?? "";
        FolderIdBox.Text = Environment.GetEnvironmentVariable("YANDEX_FOLDER_ID") ?? "";
        OutputPathBox.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WORDSTATCHEK Results");
    }

    private void ChooseInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "TXT-файлы (*.txt)|*.txt|Все файлы (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) InputPathBox.Text = dialog.FileName;
    }

    private void ChooseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выберите папку результатов", Multiselect = false };
        if (dialog.ShowDialog(this) == true) OutputPathBox.Text = dialog.FolderName;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs()) return;
        SetRunning(true);
        activity.Clear();
        cancellation = new CancellationTokenSource();
        try
        {
            var phrases = await PhraseFile.ReadAsync(InputPathBox.Text, cancellation.Token);
            if (phrases.Count == 0) throw new InvalidDataException("Во входном файле нет фраз.");
            var output = OutputPathBox.Text;
            Directory.CreateDirectory(output);
            var regions = RegionsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var device = (DeviceBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var options = new WordstatOptions
            {
                ApiKey = ApiKeyBox.Password,
                FolderId = FolderIdBox.Text,
                NumPhrases = 1,
                Regions = regions,
                Devices = string.IsNullOrWhiteSpace(device) ? [] : [device]
            };
            using var http = new HttpClient();
            var runner = new WordstatRunner(
                new WordstatApiClient(http, options),
                new CheckpointStore(System.IO.Path.Combine(output, "wordstat.checkpoint.json")),
                new JsonLineLogger(System.IO.Path.Combine(output, "wordstat.log.jsonl")));
            var progress = new Progress<ProgressUpdate>(UpdateProgress);
            StatusText.Text = "Проверка выполняется…";
            var results = await runner.RunAsync(phrases, progress, cancellation.Token);
            ExportService.Export(results, output);
            StatusText.Text = cancellation.IsCancellationRequested
                ? "Остановлено. Результаты и checkpoint сохранены."
                : "Готово. Результаты сохранены.";
            OpenResultsButton.IsEnabled = true;
        }
        catch (WordstatApiException error)
        {
            StatusText.Text = error.Message;
            MessageBox.Show(this, error.Message, "Ошибка Wordstat API", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception error)
        {
            StatusText.Text = "Запуск завершён с ошибкой.";
            MessageBox.Show(this, error.Message, "WORDSTATCHEK", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetRunning(false);
            cancellation?.Dispose();
            cancellation = null;
        }
    }

    private bool ValidateInputs()
    {
        if (!File.Exists(InputPathBox.Text)) return ShowValidation("Выберите существующий TXT-файл.");
        if (string.IsNullOrWhiteSpace(OutputPathBox.Text)) return ShowValidation("Выберите папку результатов.");
        if (string.IsNullOrWhiteSpace(ApiKeyBox.Password)) return ShowValidation("Введите Yandex Search API Key.");
        if (string.IsNullOrWhiteSpace(FolderIdBox.Text)) return ShowValidation("Введите Yandex Cloud Folder ID.");
        return true;
    }

    private bool ShowValidation(string message)
    {
        MessageBox.Show(this, message, "Проверьте настройки", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private void UpdateProgress(ProgressUpdate update)
    {
        var summary = update.Summary;
        ProcessedText.Text = $"{summary.Processed} / {summary.Total}";
        NonzeroText.Text = summary.Nonzero.ToString();
        ZeroText.Text = summary.Zero.ToString();
        ErrorsText.Text = summary.Errors.ToString();
        RunProgress.Value = summary.Total == 0 ? 0 : summary.Processed * 100d / summary.Total;
        if (update.LastResult is not null)
        {
            var count = update.LastResult.TotalCount?.ToString() ?? "ошибка";
            activity.Insert(0, $"{update.LastResult.Phrase} — {count}");
            while (activity.Count > 100) activity.RemoveAt(activity.Count - 1);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Останавливаем после текущего запроса…";
        cancellation?.Cancel();
    }

    private void OpenResults_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(OutputPathBox.Text)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", OutputPathBox.Text) { UseShellExecute = true });
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        InputPathBox.IsEnabled = !running;
        OutputPathBox.IsEnabled = !running;
        ApiKeyBox.IsEnabled = !running;
        FolderIdBox.IsEnabled = !running;
        RegionsBox.IsEnabled = !running;
        DeviceBox.IsEnabled = !running;
    }
}
