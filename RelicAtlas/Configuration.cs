using System.Collections.Generic;
using Dalamud.Configuration;
using RelicAtlas.Core;

namespace RelicAtlas;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Automatic { get; set; } = true;
    public bool HideComplete { get; set; }
    public bool PinnedOnly { get; set; }
    public string ShoppingCurrency { get; set; } = ShoppingList.AllCurrencies;
    public bool ShoppingHideComplete { get; set; } = true;
    public bool ShoppingShowSources { get; set; } = true;
    public bool AtmaArriveEarly { get; set; }
    public bool AtmaSkipCollected { get; set; } = true;
    public Dictionary<ulong, CharacterProgress> Characters { get; set; } = [];
}
