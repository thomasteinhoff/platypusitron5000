using System;
using System.IO;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

class Program
{
    // Constants
    private const string ButtonsPath = "data/buttons.json";
    private const string DialoguesPath = "data/dialogues.json";

    private static Form _mainForm;
    private static System.Windows.Forms.Timer _gameTimer;
    private static float _deltaTime;
    private static float _messageTimer = 0f;
    private static GameBalance _gamebalance = new GameBalance();
    private static PlayerData _player = new PlayerData();
    private static AvatarDisplay _avatarDisplay;
    private static List<Control> _progressControls = new List<Control>();
    private static DateTime _lastUpdateTime;
    private static DialogueData _dialogues;
    private static Label _moneyLabel;
    private static Label _levelLabel;
    private static ButtonData _cachedButtonData;
    private static bool _isDead = false;
    private static bool _isDarkTheme = true;
    private static Button _helpButton;

    private static readonly Dictionary<string, Action> ActionEffects = new Dictionary<string, Action>
    {
        ["action_peck"] = () => _player.Money += Random.Shared.Next(
            (int)(_gamebalance.PeckMoneyMin * 1000),
            (int)(_gamebalance.PeckMoneyMax * 1000)) / 1000f,

        ["action_glow"] = () => _player.Wisdom += Random.Shared.Next(
            (int)(_gamebalance.GlowWisdomMin * 100),
            (int)(_gamebalance.GlowWisdomMax * 100)) / 100f,

        ["action_poison"] = () => _player.Famine = 0f,

        ["action_smoke"] = () =>
        {
            _player.Stress -= _gamebalance.SmokeStressReduction;
            _player.Cigs--;
        },

        ["action_drink"] = () =>
        {
            _player.Health += _gamebalance.DrinkHealthIncrease;
            _player.Beers--;
        },

        ["action_gamble"] = () =>
        {
            float randomValue = (float)Random.Shared.Next(1000000);

            if (randomValue < _gamebalance.GambleJackpotChance)
            {
                _player.Money += _gamebalance.GambleJackpotAmount;
                ShowDialogues(2);
            }
            else
            {
                _player.Money -= _gamebalance.GambleLossAmount;
                _player.Stress += _gamebalance.GambleStressIncrease;
                ShowDialogues(3);
            }
        },

        ["action_pokemon"] = () =>
        {
            float randomValue = Random.Shared.NextSingle();
            float winrate = _gamebalance.PokemonBaseWinRate;
            if (_player.IsSwordEquipped) winrate += _gamebalance.PokemonSwordBonus;
            if (_player.IsShieldEquipped) winrate += _gamebalance.PokemonShieldBonus;

            if (randomValue < winrate)  // win
            {
                _player.Money += Random.Shared.Next(
                    (int)(_gamebalance.PokemonWinMoneyMin * 100),
                    (int)(_gamebalance.PokemonWinMoneyMax * 100)) / 100f;
                _player.Wisdom += _gamebalance.PokemonWinWisdomIncrease;
                _player.Health -= _gamebalance.PokemonWinHealthDecrease;
                ShowDialogues(4);
            }
            else                        // lose
            {
                _player.Stress += _gamebalance.PokemonLoseStressIncrease;
                _player.Health -= _gamebalance.PokemonLoseHealthDecrease;
                _player.Money -= Random.Shared.Next(
                    (int)(_gamebalance.PokemonLoseMoneyMin * 100),
                    (int)(_gamebalance.PokemonLoseMoneyMax * 100)) / 100f;
                ShowDialogues(5);
            }

            _player.Money = Math.Max(0, _player.Money);
            _player.Wisdom = Math.Clamp(_player.Wisdom, 0f, 1f);
            _player.Health = Math.Clamp(_player.Health, 0f, 1f);
            _player.Stress = Math.Clamp(_player.Stress, 0f, 1f);
        },

        ["action_read"] = () =>
        {
            if (_player.Level >= 7)
            {
                _player.CanRead = true;

                string walletPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallet");
                Directory.CreateDirectory(walletPath);

                string flagPath = Path.Combine(walletPath, "can_read.flag");

                File.WriteAllText(flagPath, "1");

                ShowDialogues(6);
            }
            else
            {
                ShowDialogues(7);
            }
        }
    };

    private static readonly Dictionary<string, Action> PurchaseEffects = new Dictionary<string, Action>
    {
        ["product_beer"] = () => _player.Beers++,
        ["product_cigarettes"] = () => _player.Cigs++,
        ["product_instructions"] = () => _player.HasInstructions = true,
        ["product_purse"] = () =>
        {
            _player.HasPurse = true;
            RefreshUI();
        },
        ["product_sword"] = () =>
        {
            _player.HasSword = true;
            _player.IsSwordEquipped = true;
        },
        ["product_shield"] = () =>
        {
            _player.HasShield = true;
            _player.IsShieldEquipped = true;
        },
        ["product_vision"] = () =>
        {
            _player.HasVision = true;
            RefreshUI();
        },
        ["product_brain"] = () => _player.HasMemory = true,
        ["product_reach"] = () => _player.HasMembers = true,
        ["product_acid"] = () => _player.HasStomach = true,
        ["product_property"] = () => _player.HasHouse = true
    };

    [STAThread]
    static void Main()
    {
        InitializeApplication();
        RunGameLoop();
        AvatarDisplay.CleanupStaticImages();
    }

    private static void InitializeApplication()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        _cachedButtonData = LoadJson<ButtonData>(ButtonsPath);
        _dialogues = LoadJson<DialogueData>(DialoguesPath);

        _player = new PlayerData();
        _mainForm = CreateMainForm();
        _gameTimer = new Timer { Interval = 33 };
        _gameTimer.Tick += GameUpdate;
    }

    private static T LoadJson<T>(string path)
    {
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {fullPath}");

        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath))
            ?? throw new Exception($"Failed to deserialize {path}");
    }

    private static void RunGameLoop()
    {
        _gameTimer.Start();
        Application.Run(_mainForm);
    }

    private static void GameUpdate(object sender, EventArgs e)
    {
        var now = DateTime.Now;
        _deltaTime = (float)(now - _lastUpdateTime).TotalSeconds;
        _lastUpdateTime = now;

        _deltaTime = Math.Min(_deltaTime, 0.1f);

        UpdateGameState(_deltaTime);
        UpdateButtonStates();
        RefreshUI();

        _mainForm.Invalidate();
    }

    private static void UpdateGameState(float deltaTime)
    {
        static float GetVariedTime(float baseTime)
        {
            float variation = (Random.Shared.NextSingle() * 2 - 1) * _gamebalance.VariationRange;
            return baseTime * (1 + variation);
        }

        // Update stress
        if (!_player.HasHouse)
        {
            float actualStressTime = GetVariedTime(_gamebalance.StressGainRate);
            float stressIncreasePerSecond = 1f / actualStressTime;
            _player.Stress += stressIncreasePerSecond * deltaTime;
            _player.Stress = Math.Clamp(_player.Stress, 0f, 1f);
        }

        // Update famine
        float actualFamineTime = GetVariedTime(_gamebalance.FamineGainRate);
        float famineIncreasePerSecond = 1f / actualFamineTime;
        if (!_player.HasStomach)
        {
            _player.Famine += famineIncreasePerSecond * deltaTime;
        }
        _player.Famine += famineIncreasePerSecond * deltaTime;
        _player.Famine = Math.Clamp(_player.Famine, 0f, 1f);

        // Update health
        if (_player.Famine >= 1f)
        {
            float actualHealthTime = GetVariedTime(_gamebalance.HealthDecayRate);
            float healthIncreasePerSecond = 1f / actualHealthTime;
            _player.Health -= healthIncreasePerSecond * deltaTime;
            _player.Health = Math.Clamp(_player.Health, 0f, 1f);
        }

        // Update Vigor   
        float actualVigorTime = GetVariedTime(_gamebalance.VigorDecayRate);
        float vigorIncreasePerSecond = 1f / actualVigorTime;
        _player.Vigor += vigorIncreasePerSecond * deltaTime;
        _player.Vigor = Math.Clamp(_player.Vigor, 0f, 1f);

        if (_player.Wisdom >= 1f)
        {
            _player.Level++;
            _player.Wisdom = 0f;
        }

        if (_player.Stress >= 1f || _player.Health <= 0) HeartAttack();

        _messageTimer += deltaTime;
        if (_messageTimer >= 10f)
        {
            _messageTimer = 0;
            ShowDialogues(0);
        }
    }

    static Form CreateMainForm()
    {
        var form = new Form
        {
            Text = "platypor",
            ClientSize = new Size(775, 610),
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox = false,
            BackColor = ColorTranslator.FromHtml("#222")
        };

        _helpButton = new Button
        {
            Text = "?",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.DodgerBlue,
            Size = new Size(25, 25),
            Font = new Font("Arial", 10, FontStyle.Bold),
            Location = new Point(form.ClientSize.Width - 30, 5),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TabStop = false,
            Visible = false
        };

        _helpButton.Click += (sender, e) =>
        {
            string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "instructionsNote.bat");
            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                WorkingDirectory = Path.GetDirectoryName(batPath),
                UseShellExecute = true
            });
        };

        System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddEllipse(0, 0, _helpButton.Width, _helpButton.Height);
        _helpButton.Region = new Region(path);

        form.Controls.Add(_helpButton);
        _helpButton.BringToFront();

        _avatarDisplay = new AvatarDisplay(_player) { AvatarBox = { Location = new Point(10, 10) } };
        var (chatBox, speechTail) = CreateChatComponents(form, _avatarDisplay.AvatarBox);
        _progressControls = CreateProgressBarList(_avatarDisplay.AvatarBox);
        var controlPanel = CreateControlPanel(form, chatBox, 20, 20);

        var actionsSection = CreateActionsSection(form, _progressControls.Last());
        var purchasablesSection = CreatePurchasablesSection(form, actionsSection);

        form.Controls.Add(_avatarDisplay.AvatarBox);
        form.Controls.Add(chatBox);
        form.Controls.Add(speechTail);
        form.Controls.AddRange(_progressControls.ToArray());
        form.Controls.Add(controlPanel);
        form.Controls.Add(actionsSection);
        form.Controls.Add(purchasablesSection);

        return form;
    }

    static void RefreshUI()
    {
        if (_moneyLabel == null || _levelLabel == null || _mainForm == null)
        {
            MessageBox.Show("UI not ready - skipping refresh");
            return;
        }

        if (_helpButton != null)
        {
            _helpButton.Visible = _player.HasInstructions;
        }

        _moneyLabel.Text = $"Money: {_player.Money}";
        _levelLabel.Text = $"Level: {_player.Level}";

        // Update control panel visibility and consumables
        foreach (Control control in _mainForm.Controls)
        {
            if (control is Panel panel && panel.Controls.Count > 0 && panel.Controls[0] is Button)
            {
                foreach (Control c in panel.Controls)
                {
                    switch (c.Tag as string)
                    {
                        // Equipment toggles
                        case "toggle_sword":
                            c.Visible = _player.HasPurse;
                            c.Enabled = _player.HasSword;
                            break;
                        case "toggle_shield":
                            c.Visible = _player.HasPurse;
                            c.Enabled = _player.HasShield;
                            break;

                        // Consumable displays
                        case "beers_label":
                            c.Visible = _player.HasPurse;
                            c.Text = $"Beers: {_player.Beers}";
                            break;
                        case "cigs_label":
                            c.Visible = _player.HasPurse;
                            c.Text = $"Cigarettes: {_player.Cigs}";
                            break;

                        // Special buttons
                        case "save_button":
                            c.Enabled = _player.HasMemory;
                            break;

                        // Theme button
                        case null when c is Button btn && btn.Text == "Theme":
                            btn.Enabled = _player.HasVision;
                            break;
                    }
                }
            }
        }

        // Update progress bars
        var stats = new (string Name, float Value)[]
        {
        ("Stress", _player.Stress),
        ("Famine", _player.Famine),
        ("Health", _player.Health),
        ("Wisdom", _player.Wisdom),
        ("Vigor", _player.Vigor)
        };

        for (int i = 0; i < stats.Length; i++)
        {
            if (_progressControls[i * 2] is Panel progressBg &&
                progressBg.Controls.Count > 0 &&
                progressBg.Controls[0] is Panel progressFill)
            {
                int fillHeight = (int)(progressBg.Height * stats[i].Value);
                progressFill.Size = new Size(progressBg.Width, fillHeight);
                progressFill.Location = new Point(0, progressBg.Height - fillHeight);
            }
        }

        // Update avatar
        _avatarDisplay.UpdateAvatar();
    }

    static (RichTextBox chatBox, Label speechTail) CreateChatComponents(Form form, PictureBox avatarBox)
    {
        int chatWidth = form.ClientSize.Width - avatarBox.Right - 20;
        int chatHeight = avatarBox.Height;

        var chatBox = new RichTextBox
        {
            Location = new Point(avatarBox.Right + 10, avatarBox.Top),
            Size = new Size(chatWidth, chatHeight),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Comic Sans MS", 12),
            ReadOnly = true,
            BackColor = Color.White,
            ForeColor = Color.Black,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Enabled = false,
            Margin = new Padding(5),
            WordWrap = true
        };

        var speechTail = new Label
        {
            Text = "◀",
            Font = new Font("Arial", 24),
            ForeColor = chatBox.BackColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(
                avatarBox.Right - 8,
                avatarBox.Top + chatHeight - 40)
        };

        return (chatBox, speechTail);
    }

    private static string GetRandomDialogue()
    {
        int randomIndex = Random.Shared.Next(_dialogues.VoiceLines.Count);
        return _dialogues.VoiceLines[randomIndex].Text;
    }

    private static void ShowDialogues(int messageType)
    {
        string message;

        switch (messageType)
        {
            case 0:
                message = GetRandomDialogue();
                break;
            case 1:
                message = "This is it, huh? Uninstalled by life itself. Godspeed, cursor tyrant.";
                break;
            case 2:
                message = "Statistically improbable. Emotionally disappointing.";
                break;
            case 3:
                message = "Shockingly, betting on chaos didn’t pay off. Again.";
                break;
            case 4:
                message = "Victory. With nothing but my flippers and existential dread.";
                break;
            case 5:
                message = "Turns out rage isn't super effective against everything.";
                break;
            case 6:
                message = "Ah, literacy. Another illusion of freedom unlocked.";
                break;
            case 7:
                message = "Apparently, ignorance is my level cap.";
                break;
            default:
                message = "...";
                break;
        }

        foreach (Control control in Application.OpenForms[0].Controls)
        {
            if (control is RichTextBox chatBox)
            {
                chatBox.Text = message;
                break;
            }
        }
    }

    static List<Control> CreateProgressBarList(PictureBox avatarBox)
    {
        var controls = new List<Control>();
        int yPosition = avatarBox.Bottom + 20;
        int barWidth = avatarBox.Width;
        int barHeight = 20;
        int spacing = 5;

        string[] statNames = { "Stress", "Famine", "Health", "Wisdom", "Vigor" };

        foreach (var name in statNames)
        {
            var progressBg = new Panel
            {
                Location = new Point(avatarBox.Left, yPosition),
                Size = new Size(barWidth, barHeight),
                BackColor = Color.FromArgb(50, 50, 50),
                BorderStyle = BorderStyle.FixedSingle
            };

            progressBg.Controls.Add(new Panel
            {
                Size = new Size(barWidth, 0),
                BackColor = Color.FromArgb(100, 200, 100),
                Location = new Point(0, barHeight)
            });

            controls.Add(progressBg);

            controls.Add(new Label
            {
                Text = name,
                Location = new Point(progressBg.Right + 10, yPosition),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Arial", 9)
            });

            yPosition += barHeight + spacing;
        }

        return controls;
    }

    static Panel CreateControlPanel(Form form, RichTextBox chatBox, int rightMargin, int topMargin)
    {
        const int elementSpacing = 15;
        const int lineHeight = 35;

        var controlPanel = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent,
            AutoSize = true,
            Height = lineHeight * 2
        };

        var swordButton = new Button
        {
            Text = "Switch Sword",
            Size = new Size(100, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = ColorTranslator.FromHtml("#444"),
            Enabled = _player.HasSword,
            Visible = _player.HasPurse,
            Tag = "toggle_sword"
        };
        swordButton.Click += ToggleEquipment_Click;

        var shieldButton = new Button
        {
            Text = "Switch Shield",
            Size = new Size(100, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = ColorTranslator.FromHtml("#444"),
            Enabled = _player.HasShield,
            Visible = _player.HasPurse,
            Tag = "toggle_shield"
        };
        shieldButton.Click += ToggleEquipment_Click;

        var themeButton = new Button
        {
            Text = "Theme",
            Size = new Size(90, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = ColorTranslator.FromHtml("#444"),
            Enabled = _player.HasVision,
            Visible = true
        };
        themeButton.Click += (sender, e) =>
        {
            _isDarkTheme = !_isDarkTheme;
            _mainForm.BackColor = _isDarkTheme ? ColorTranslator.FromHtml("#222") : ColorTranslator.FromHtml("#89CFF0");
        };

        var saveButton = new Button
        {
            Text = "Save",
            Size = new Size(70, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = ColorTranslator.FromHtml("#444"),
            Enabled = _player.HasMemory,
            Visible = true,
            Tag = "save_button"
        };

        var loadButton = new Button
        {
            Text = "Load",
            Size = new Size(70, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = ColorTranslator.FromHtml("#444"),
            Enabled = true,
            Visible = true
        };

        var beersLabel = new Label
        {
            Text = $"Beers: {_player.Beers}",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Arial", 10, FontStyle.Bold),
            Visible = _player.HasPurse,
            Tag = "beers_label"
        };

        var cigsLabel = new Label
        {
            Text = $"Cigarettes: {_player.Cigs}",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Arial", 10, FontStyle.Bold),
            Visible = _player.HasPurse,
            Tag = "cigs_label"
        };

        _levelLabel = new Label
        {
            Text = $"Level: {_player.Level}",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Arial", 10, FontStyle.Bold),
            Visible = true
        };

        _moneyLabel = new Label
        {
            Text = $"Money: {_player.Money}",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Arial", 10, FontStyle.Bold),
            Visible = true
        };

        controlPanel.Controls.Add(swordButton);
        controlPanel.Controls.Add(shieldButton);
        controlPanel.Controls.Add(themeButton);
        controlPanel.Controls.Add(saveButton);
        controlPanel.Controls.Add(loadButton);
        controlPanel.Controls.Add(beersLabel);
        controlPanel.Controls.Add(cigsLabel);
        controlPanel.Controls.Add(_levelLabel);
        controlPanel.Controls.Add(_moneyLabel);

        int currentX = 0;
        swordButton.Location = new Point(currentX, 5);
        currentX += swordButton.Width + elementSpacing;

        shieldButton.Location = new Point(currentX, 5);
        currentX += shieldButton.Width + elementSpacing;

        themeButton.Location = new Point(currentX, 5);
        currentX += themeButton.Width + elementSpacing;

        saveButton.Location = new Point(currentX, 5);
        currentX += saveButton.Width + elementSpacing;

        loadButton.Location = new Point(currentX, 5);

        currentX = 0;
        beersLabel.Location = new Point(currentX, lineHeight + 10);
        currentX += beersLabel.Width + elementSpacing;

        cigsLabel.Location = new Point(currentX, lineHeight + 10);
        currentX += cigsLabel.Width + elementSpacing;

        _levelLabel.Location = new Point(currentX, lineHeight + 10);
        currentX += _levelLabel.Width + elementSpacing;

        _moneyLabel.Location = new Point(currentX, lineHeight + 10);

        int line1Width = swordButton.Width + shieldButton.Width + themeButton.Width +
                        saveButton.Width + loadButton.Width + 4 * elementSpacing;
        int line2Width = beersLabel.Width + cigsLabel.Width + _levelLabel.Width +
                        _moneyLabel.Width + 3 * elementSpacing;
        int panelWidth = Math.Max(line1Width, line2Width);

        controlPanel.Width = panelWidth;
        controlPanel.Location = new Point(
            form.ClientSize.Width - controlPanel.Width - rightMargin,
            chatBox.Bottom + topMargin
        );

        return controlPanel;
    }

    private static void ToggleEquipment_Click(object sender, EventArgs e)
    {
        var button = (Button)sender;
        string buttonType = (string)button.Tag;

        switch (buttonType)
        {
            case "toggle_sword":
                _player.IsSwordEquipped = !_player.IsSwordEquipped;
                break;
            case "toggle_shield":
                _player.IsShieldEquipped = !_player.IsShieldEquipped;
                break;
        }

        _avatarDisplay.UpdateAvatar();
    }

    static void HeartAttack()
    {
        ShowDialogues(1);

        if (_isDead) return;

        _isDead = true;

        for (int i = 0; i <= 5; i++)
        {
            MessageBox.Show("You had a heart attack and died.",
                           "Game Over",
                           MessageBoxButtons.OK,
                           MessageBoxIcon.Error);
        }

        Environment.Exit(666);
    }

    // ------------------------------------
    // --- Actions and Products Buttons ---
    // ------------------------------------

    static Panel CreateSectionPanel(Form form, Control aboveControl, string title)
    {
        var panel = new Panel
        {
            Location = new Point(10, aboveControl.Bottom + 10),
            Size = new Size(form.ClientSize.Width - 20, 150),
            BackColor = Color.Transparent,
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Arial", 10, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(10, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        panel.Controls.Add(titleLabel);

        var scrollPanel = new Panel
        {
            Location = new Point(0, titleLabel.Bottom + 10),
            Size = new Size(panel.Width, panel.Height - titleLabel.Bottom - 10),
            AutoScroll = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        scrollPanel.VerticalScroll.Enabled = false;
        scrollPanel.VerticalScroll.Visible = false;
        scrollPanel.HorizontalScroll.Enabled = true;
        scrollPanel.HorizontalScroll.Visible = true;

        panel.Controls.Add(scrollPanel);
        return panel;
    }

    private static Panel CreateActionsSection(Form form, Control aboveControl)
    {
        var panel = CreateSectionPanel(form, aboveControl, "ACTIONS");
        var scrollPanel = (Panel)panel.Controls[1];

        int buttonMargin = 10;
        int currentLeft = buttonMargin;

        foreach (var actionButton in _cachedButtonData.ActionButtons)
        {
            var button = new GameButton(
                id: actionButton.Id,
                text: actionButton.Text
            );

            button.Click += HandleButtonAction;
            button.Location = new Point(currentLeft, 10);
            scrollPanel.Controls.Add(button);
            currentLeft += button.Width + buttonMargin;
        }

        scrollPanel.AutoScrollMinSize = new Size(currentLeft, scrollPanel.Height);
        return panel;
    }

    private static Panel CreatePurchasablesSection(Form form, Control aboveControl)
    {
        var panel = CreateSectionPanel(form, aboveControl, "PURCHASABLES");
        var scrollPanel = (Panel)panel.Controls[1];

        int buttonMargin = 10;
        int currentLeft = buttonMargin;

        foreach (var productButton in _cachedButtonData.ProductButtons)
        {
            var button = new GameButton(
                id: productButton.Id,
                text: productButton.Text,
                price: (float)productButton.Price,
                isProduct: true
            );

            button.Click += HandleButtonAction;
            button.Location = new Point(currentLeft, 10);
            scrollPanel.Controls.Add(button);
            currentLeft += button.Width + buttonMargin;
        }

        scrollPanel.AutoScrollMinSize = new Size(currentLeft, scrollPanel.Height);
        return panel;
    }

    private static void UpdateButtonStates()
    {
        bool hasFullVigor = _player.Vigor >= 1.0f;

        foreach (var button in FindButtonsByTagPrefix("action_"))
        {
            bool enabled = hasFullVigor;

            switch (button.Tag as string)
            {
                case "action_drink":
                    enabled &= _player.HasPurse && _player.Beers > 0;
                    break;
                case "action_smoke":
                    enabled &= _player.HasPurse && _player.Cigs > 0;
                    break;
                case "action_read":
                    enabled &= !_player.CanRead;
                    break;
            }

            button.Enabled = enabled;
        }

        foreach (var button in FindButtonsByTagPrefix("product_"))
        {
            var product = _cachedButtonData.ProductButtons.First(p => p.Id == (string)button.Tag);
            button.Enabled = CanPurchaseProduct(product);
        }
    }

    private static IEnumerable<Button> FindButtonsByTagPrefix(string prefix)
    {
        return Application.OpenForms[0].Controls
            .OfType<Panel>()
            .SelectMany(p => p.Controls.OfType<Panel>())
            .SelectMany(p => p.Controls.OfType<Button>())
            .Where(b => b.Tag is string tag && tag.StartsWith(prefix));
    }

    private static bool IsNonConsumableOwned(string productId)
    {
        switch (productId)
        {
            case "product_instructions": return _player.HasInstructions;
            case "product_purse": return _player.HasPurse;
            case "product_sword": return _player.HasSword;
            case "product_shield": return _player.HasShield;
            case "product_vision": return _player.HasVision;
            case "product_brain": return _player.HasMemory;
            case "product_reach": return _player.HasMembers;
            case "product_acid": return _player.HasStomach;
            case "product_property": return _player.HasHouse;
            default: return false;
        }
    }

    private static void HandleButtonAction(object sender, EventArgs e)
    {
        if (!(sender is GameButton button) || _player.Vigor < 1.0f)
            return;

        _player.Vigor = 0f;

        if (button.IsProduct)
            HandleProductPurchase(button);
        else
            HandleGameAction(button.ActionId);

        RefreshUI();
    }

    private static void HandleGameAction(string actionId)
    {
        if (ActionEffects.TryGetValue(actionId, out var effect))
        {
            switch (actionId)
            {
                case "action_drink" when _player.Beers <= 0:
                case "action_smoke" when _player.Cigs <= 0:
                    return;
                case "action_gamble" when (_player.IsSwordEquipped || _player.IsShieldEquipped):
                    MessageBox.Show("You can't enter the casino armed!",
                                  "Security Alert",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return;
            }

            effect();
            RefreshUI();
        }
    }

    private static void HandleProductPurchase(GameButton button)
    {
        var product = _cachedButtonData.ProductButtons.FirstOrDefault(p => p.Id == button.ActionId);
        if (product == null || !CanPurchaseProduct(product))
            return;

        _player.Money -= (float)product.Price;
        ApplyPurchaseEffect(product.Id);
        RefreshUI();
    }

    private static bool CanPurchaseProduct(ProductButton product)
    {
        if (product.Id == "product_freedom")
            return Math.Abs(_player.Money - 48750.0f) < 0.001f;

        var purseRequiredItems = new HashSet<string> {
        "product_beer",
        "product_cigarettes",
        "product_sword",
        "product_shield"
    };

        bool purseRequired = purseRequiredItems.Contains(product.Id);
        bool hasPurseRequirement = !purseRequired || _player.HasPurse;

        return _player.Money >= product.Price &&
               !IsNonConsumableOwned(product.Id) &&
               hasPurseRequirement;
    }

    private static void ApplyPurchaseEffect(string productId)
    {
        if (PurchaseEffects.TryGetValue(productId, out var effect))
        {
            effect();
        }
    }
}