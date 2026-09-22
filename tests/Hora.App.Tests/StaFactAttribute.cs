namespace Hora.App.Tests;

/// <summary>
/// Markiert Tests, die einen STA-Thread brauchen (WPF-Typen wie ResourceDictionary). Das Attribut
/// allein zwingt den xUnit-Testläufer nicht auf einen STA-Thread; die eigentliche Ausführung im
/// STA-Thread übernimmt <see cref="ThemeManagerTests"/> selbst über einen selbst erzeugten Thread
/// mit <see cref="System.Threading.ApartmentState.STA"/>. Das Attribut dient hier der Dokumentation
/// der Absicht und hält sich an die Vorgabe aus dem Task-Brief.
/// </summary>
public sealed class StaFactAttribute : FactAttribute
{
}
