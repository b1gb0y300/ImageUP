using System.Collections.Generic;
using System.Windows.Media;

namespace ImageEnhancementWpf
{
    public class MethodGroup
    {
        public string CategoryName { get; set; } = "";
        public List<MethodItem> Items { get; set; } = new List<MethodItem>();
        public Brush CategoryColor { get; set; } = Brushes.Gray;
    }
}
