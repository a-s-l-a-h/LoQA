using LoQA.Services;

namespace LoQA.Views;

public partial class HomePage : ContentPage
{
    private readonly EasyChatService _chatService;
    private bool _isSidebarVisible = true;

    public HomePage(EasyChatService chatService)
    {
        InitializeComponent();
        _chatService = chatService;

        // Set the binding context for this page and its children (the ContentView's)
        this.BindingContext = _chatService;

        // Subscribe to the toggle event raised by the chat view
        ChatContentViewContent.ToggleSidebarRequested += OnToggleSidebarRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _chatService.LoadConversationsFromDbAsync();
    }

    private void OnToggleSidebarRequested(object sender, EventArgs e)
    {
        _isSidebarVisible = !_isSidebarVisible;
        SidebarColumn.Width = _isSidebarVisible ? new GridLength(320) : new GridLength(0);
    }
}