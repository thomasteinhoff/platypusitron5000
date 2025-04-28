using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class GameBalance
{
    // Existing properties
    public float HealthDecayRate { get; } = 10f;
    public float VigorDecayRate { get; } = 1.5f;
    public float StressGainRate { get; } = 120f;
    public float FamineGainRate { get; } = 60f;
    public float VariationRange { get; } = 0.1f;

    // New properties for action effects
    public float PeckMoneyMin { get; } = 3f;
    public float PeckMoneyMax { get; } = 9f;

    public float GlowWisdomMin { get; } = 0.03f;
    public float GlowWisdomMax { get; } = 0.06f;

    public float SmokeStressReduction { get; } = 0.28f;
    public float DrinkHealthIncrease { get; } = 0.25f;

    public float GambleJackpotChance { get; } = 3f;
    public float GambleJackpotAmount { get; } = 100000f;
    public float GambleLossAmount { get; } = 10f;
    public float GambleStressIncrease { get; } = 0.5f;

    public float PokemonBaseWinRate { get; } = 0.5f;
    public float PokemonSwordBonus { get; } = 0.2f;
    public float PokemonShieldBonus { get; } = 0.1f;

    public float PokemonWinMoneyMin { get; } = 15f;
    public float PokemonWinMoneyMax { get; } = 45f;
    public float PokemonWinWisdomIncrease { get; } = 0.334f;
    public float PokemonWinHealthDecrease { get; } = 0.15f;

    public float PokemonLoseMoneyMin { get; } = 30f;
    public float PokemonLoseMoneyMax { get; } = 60f;
    public float PokemonLoseStressIncrease { get; } = 0.3f;
    public float PokemonLoseHealthDecrease { get; } = 0.25f;
}

public class PlayerData
{
    // Stats
    public float Stress { get; set; } = 0f;
    public float Famine { get; set; } = 0f;
    public float Health { get; set; } = 1f;
    public float Wisdom { get; set; } = 0f;
    public float Vigor { get; set; } = 1f;
    public float Money { get; set; } = 42353436f;
    public int Level { get; set; } = 1;

    // Inventory
    public int Beers { get; set; }
    public int Cigs { get; set; }

    // Equipment
    public bool HasSword { get; set; }
    public bool IsSwordEquipped { get; set; }
    public bool HasShield { get; set; }
    public bool IsShieldEquipped { get; set; }

    // Attributes
    public bool CanRead { get; set; }
    public bool HasPurse { get; set; }
    public bool HasInstructions { get; set; }
    public bool HasVision { get; set; }
    public bool HasMemory { get; set; }
    public bool HasMembers { get; set; }
    public bool HasStomach { get; set; }
    public bool HasHouse { get; set; }

    public int AvatarState => (HasSword ? 1 : 0) + (HasShield ? 2 : 0);
}

public class DialogueLine
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public class DialogueData
{
    [JsonPropertyName("voice_lines")]
    public List<DialogueLine> VoiceLines { get; set; }
}

public class ButtonData
{
    [JsonPropertyName("actionButtons")]
    public List<ActionButton> ActionButtons { get; set; }

    [JsonPropertyName("productButtons")]
    public List<ProductButton> ProductButtons { get; set; }
}

public class ActionButton
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public class ProductButton
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("price")]
    public double Price { get; set; }
}

public class GameButton : Button
{
    public string ActionId { get; }
    public float Price { get; }
    public bool IsProduct { get; }

    public GameButton(string id, string text, float price = 0, bool isProduct = false)
    {
        ActionId = id;
        Text = isProduct ? $"{text}{Environment.NewLine}$ {price}" : text;
        Price = price;
        IsProduct = isProduct;
        Tag = id;

        Size = new Size(120, 60);
        FlatStyle = FlatStyle.Flat;
        ForeColor = Color.White;
        BackColor = ColorTranslator.FromHtml("#444");
        TabStop = false;
    }
}

public class AvatarDisplay : IDisposable
{
    private readonly PictureBox _avatarBox;
    private readonly PlayerData _player;
    private static readonly Dictionary<string, Image> _imageCache = new Dictionary<string, Image>();
    private static readonly object _imageLock = new object();

    public PictureBox AvatarBox => _avatarBox;

    public AvatarDisplay(PlayerData player)
    {
        _player = player;
        _avatarBox = new PictureBox
        {
            Size = new Size(150, 131),
            BackColor = Color.Transparent,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        LoadImages();
        UpdateAvatar();
    }

    private void LoadImages()
    {
        lock (_imageLock)
        {
            if (_imageCache.Count == 0)
            {
                _imageCache["base"] = LoadImage("assets/sprites/SerenePandemonium.png");
                _imageCache["sword"] = LoadImage("assets/sprites/SwordOfEternalMadness.png");
                _imageCache["shield"] = LoadImage("assets/sprites/ShieldOfTheSacreReflection.png");
            }
        }
    }

    private Image LoadImage(string path)
    {
        try
        {
            return Image.FromFile(path) ?? CreatePlaceholderImage(path);
        }
        catch
        {
            return CreatePlaceholderImage(path);
        }
    }

    public void UpdateAvatar()
    {
        using (var newImage = ComposeAvatar())
        {
            _avatarBox.Image?.Dispose();
            _avatarBox.Image = new Bitmap(newImage);
        }
    }

    private Image ComposeAvatar()
    {
        var composite = new Bitmap(150, 131);
        using (var g = Graphics.FromImage(composite))
        {
            if (_player.HasShield && _player.IsShieldEquipped)
                g.DrawImage(_imageCache["shield"], 0, 0, 150, 131);

            g.DrawImage(_imageCache["base"], 0, 0, 150, 131);

            if (_player.HasSword && _player.IsSwordEquipped)
                g.DrawImage(_imageCache["sword"], 0, 0, 150, 131);
        }
        return composite;
    }

    private Image CreatePlaceholderImage(string imageName)
    {
        var img = new Bitmap(150, 131);
        using (var g = Graphics.FromImage(img))
        {
            g.Clear(Color.Magenta);
            g.DrawString($"Missing: {imageName}", new Font("Arial", 8), Brushes.Black, 10, 10);
        }
        return img;
    }

    public void Dispose()
    {
        _avatarBox.Image?.Dispose();
    }

    public static void CleanupStaticImages()
    {
        lock (_imageLock)
        {
            foreach (var image in _imageCache.Values)
            {
                image?.Dispose();
            }
            _imageCache.Clear();
        }
    }
}

public class NoScrollOnFocusPanel : Panel
{
    protected override Point ScrollToControl(Control activeControl)
    {
        return this.AutoScrollPosition;
    }
}