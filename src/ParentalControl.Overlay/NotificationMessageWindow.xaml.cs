using System.Media;
using System.Windows;
using System.Windows.Input;

namespace ParentalControl.Overlay;

public partial class NotificationMessageWindow : Window
{
    public NotificationMessageWindow(string message)
    {
        InitializeComponent();
        TxtMessage.Text = message;

        // Check if message is predominantly Arabic for RTL
        var isArabic = message.Any(c => c >= 0x0600 && c <= 0x06FF);
        this.FlowDirection = isArabic ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
        if (isArabic)
        {
            TxtTitle.Text = "رسالة من ولي الأمر";
        }

        MouseDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter) Close();
        };

        Loaded += (_, _) =>
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch
            {
                // Ignore audio failure
            }
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
