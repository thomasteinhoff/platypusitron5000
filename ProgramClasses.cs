using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class GameBalance
{
    public float HealthDecayRate { get; } = 10f;  // seconds to min health
    public float VigorDecayRate { get; } = 1.5f;  // seconds to max vigor
    public float StressGainRate { get; } = 120f;  // seconds to max stress
    public float FamineGainRate { get; } = 60f;   // seconds to max famine
    public float variationRange { get; } = 0.1f;  // Random variation range
}

public class PlayerData
{
    public float Stress { get; set; } = 0f;
    public float Famine { get; set; } = 0f;
    public float Health { get; set; } = 1f;
    public float Wisdom { get; set; } = 0f;
    public float Vigor { get; set; } = 1f;
    public float Money { get; set; } = 0;
    public int Level { get; set; } = 1;

    // Inventory
    public int Beers { get; set; } = 0;
    public int Cigs { get; set; } = 0;

    // Knowledge
    public bool CanRead { get; set; } = false;
    public bool HasPurse { get; set; } = false;

    // Equipment
    public bool HasSword { get; set; } = false;
    public bool IsSwordEquipped { get; set; } = false;

    public bool HasShield { get; set; } = false;
    public bool IsShieldEquipped { get; set; } = false;

    // Attributes
    public bool HasInstructions { get; set; } = false;
    public bool HasVision { get; set; } = false;
    public bool HasMemory { get; set; } = false;
    public bool HasMembers { get; set; } = false;
    public bool HasStomach { get; set; } = false;
    public bool HasHouse { get; set; } = false;

    // Computed property
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
    private PlayerData _player;

    // Image cache
    private static Image _baseImage;
    private static Image _shieldImage;
    private static Image _swordImage;
    private static readonly object _imageLock = new object();

    // State tracking
    private int _lastAvatarState;
    private bool _lastSwordEquipped;
    private bool _lastShieldEquipped;

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
        LoadBaseImages();
        UpdateAvatar();
    }

    private void LoadBaseImages()
    {
        try
        {
            _baseImage = Image.FromFile("assets/sprites/SerenePandemonium.png")
                ?? CreatePlaceholderImage();
        }
        catch
        {
            _baseImage = CreatePlaceholderImage();
        }
    }

    public void UpdateAvatar()
    {
        if (!NeedsImageUpdate()) return;

        using (var newImage = ComposeAvatar())
        {
            _avatarBox.Image?.Dispose();
            _avatarBox.Image = new Bitmap(newImage); // Create new bitmap for PictureBox
        }

        // Update tracking state
        _lastAvatarState = _player.AvatarState;
        _lastSwordEquipped = _player.IsSwordEquipped;
        _lastShieldEquipped = _player.IsShieldEquipped;
    }

    private bool NeedsImageUpdate()
    {
        return _avatarBox.Image == null ||
               _lastAvatarState != _player.AvatarState ||
               _lastSwordEquipped != _player.IsSwordEquipped ||
               _lastShieldEquipped != _player.IsShieldEquipped;
    }

    private Image ComposeAvatar()
    {
        if (_player.AvatarState == 0 && !_player.IsSwordEquipped && !_player.IsShieldEquipped)
            return new Bitmap(_baseImage);

        var composite = new Bitmap(150, 131);
        using (var g = Graphics.FromImage(composite))
        {
            if (_player.HasShield && _player.IsShieldEquipped)
                g.DrawImage(_shieldImage, 0, 0, 150, 131);

            g.DrawImage(_baseImage, 0, 0, 150, 131);

            if (_player.HasSword && _player.IsSwordEquipped)
                g.DrawImage(_swordImage, 0, 0, 150, 131);
        }
        return composite;
    }

    private Image CreatePlaceholderImage()
    {
        var img = new Bitmap(150, 131);
        using (var g = Graphics.FromImage(img))
        {
            g.Clear(Color.Magenta);
            g.DrawString("Image Missing", new Font("Arial", 10), Brushes.Black, 10, 10);
        }
        return img;
    }

    public void Dispose()
    {
        _avatarBox.Image?.Dispose();
    }

    public static void CleanupStaticImages()
    {
        _baseImage?.Dispose();
        _shieldImage?.Dispose();
        _swordImage?.Dispose();
        _baseImage = _shieldImage = _swordImage = null;
    }
}

class NoScrollOnFocusPanel : Panel
{
    protected override System.Drawing.Point ScrollToControl(Control activeControl)
    {
        return this.AutoScrollPosition;
    }
}