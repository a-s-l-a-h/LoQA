using LoQA.Services;

namespace LoQA.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(DatabaseService databaseService) // Constructor may still be passed this by DI
    {
        InitializeComponent();
    }

    private async void BackButton_Clicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}