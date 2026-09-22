using Xunit;

// Zwei Testklassen erzeugen bei Bedarf das eine erlaubte WPF-Application-Objekt (ThemeManagerTests
// und ThemeManagerSystemThemeTests). Liefen sie gleichzeitig, könnten beide die Prüfung
// „Application.Current is null“ bestehen und der zweite Aufruf scheiterte mit „Cannot create more
// than one System.Windows.Application instance“. Die Testläufe dieser Assembly laufen deshalb
// nacheinander; sie dauern zusammen deutlich unter einer Sekunde.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
