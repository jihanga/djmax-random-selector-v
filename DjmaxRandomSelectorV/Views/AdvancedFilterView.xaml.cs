using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DjmaxRandomSelectorV.Models;
using DjmaxRandomSelectorV.ViewModels;

namespace DjmaxRandomSelectorV.Views
{
    /// <summary>
    /// AdvancedFilterView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AdvancedFilterView : UserControl
    {
        public AdvancedFilterView()
        {
            InitializeComponent();
        }

        // 더블클릭된 항목의 DataContext는 PlaylistItem이므로, Caliburn 액션 바인딩 대신
        // 원본 이벤트에서 ListBoxItem을 직접 찾아 ViewModel을 호출한다.
        private void OnPlaylistItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not AdvancedFilterViewModel viewModel)
            {
                return;
            }
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source is not null and not ListBoxItem)
            {
                source = VisualTreeHelper.GetParent(source);
            }
            if (source is ListBoxItem listBoxItem && listBoxItem.DataContext is PlaylistItem item)
            {
                viewModel.SetNextPointer(item);
            }
        }
    }
}
