using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Utils;

namespace Khjin.ShipRudder
{
    public class ShipRudderSettings : ModManager
    {
        private const string SECTION_NAME = "Ship Rudder Settings";
        private const string SETTINGS_FILENAME = "ship_rudder_settings.cfg";
        private MyIni iniUtil = null;

        private const string configversion = "1.2.1";
        public bool allowvanillagyros;
        public float maxpitchangle;
        public float maxrollangle;
        public float correctionspeed;
        public double minspeedtoturnmps;
        public float maxturnspeed;
        public float minturnspeedmodifier;
        public float maxturnspeedmodifier;

        private Dictionary<string, SettingLimits> settingLimits;

        public ShipRudderSettings()
        {
            iniUtil = new MyIni();
            settingLimits = new Dictionary<string, SettingLimits>();
        }

        public override void LoadData()
        {
            settingLimits.Add(nameof(allowvanillagyros), new BoolLimits()
            {
                DefaultValue = true
            });

            settingLimits.Add(nameof(maxpitchangle), new FloatLimits() {
                DefaultValue = 15.0f,
                MinValue = 0,
                MaxValue = 45
            });

            settingLimits.Add(nameof(maxrollangle), new FloatLimits() {
                DefaultValue = 7.0f,
                MinValue = 0,
                MaxValue = 45
            });

            settingLimits.Add(nameof(correctionspeed), new FloatLimits() {
                DefaultValue = 0.15f,
                MinValue = 0.01f,
                MaxValue = 0.5f
            });

            settingLimits.Add(nameof(minspeedtoturnmps), new DoubleLimits() {
                DefaultValue = 2,
                MinValue = 1,
                MaxValue = 105
            });

            settingLimits.Add(nameof(maxturnspeed), new FloatLimits() {
                DefaultValue = 0.2f,
                MinValue = 0.01f,
                MaxValue = 0.5f
            });

            settingLimits.Add(nameof(minturnspeedmodifier), new FloatLimits() {
                DefaultValue = 0.2f,
                MinValue = 0.01f,
                MaxValue = 1.0f
            });

            settingLimits.Add(nameof(maxturnspeedmodifier), new FloatLimits() {
                DefaultValue = 1.0f,
                MinValue = 0.01f,
                MaxValue = 1.0f
            });

            // Note: This must go AFTER defining the limits as defaults
            // will also come from the limits data.
            LoadSettings();
        }

        public override void UnloadData()
        {
            SaveSettings();

            iniUtil.Clear();
            iniUtil = null;

            settingLimits.Clear();
            settingLimits = null;
        }

        public void ResetSettings()
        {
            allowvanillagyros       = ((BoolLimits)settingLimits[nameof(allowvanillagyros)]).DefaultValue;
            maxpitchangle           = ((FloatLimits)settingLimits[nameof(maxpitchangle)]).DefaultValue;
            maxrollangle            = ((FloatLimits)settingLimits[nameof(maxrollangle)]).DefaultValue;
            correctionspeed         = ((FloatLimits)settingLimits[nameof(correctionspeed)]).DefaultValue;
            minspeedtoturnmps       = ((DoubleLimits)settingLimits[nameof(minspeedtoturnmps)]).DefaultValue;
            maxturnspeed            = ((FloatLimits)settingLimits[nameof(maxturnspeed)]).DefaultValue;
            minturnspeedmodifier    = ((FloatLimits)settingLimits[nameof(minturnspeedmodifier)]).DefaultValue;
            maxturnspeedmodifier    = ((FloatLimits)settingLimits[nameof(maxturnspeedmodifier)]).DefaultValue;
        }

        private void LoadSettings()
        {
            try
            {
                // Search settings in the world save
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(SETTINGS_FILENAME, typeof(ShipRudderSettings)))
                {
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(SETTINGS_FILENAME, typeof(ShipRudderSettings));
                    string settingsData = reader.ReadToEnd();

                    iniUtil.Clear();
                    if (iniUtil.TryParse(settingsData))
                    {
                        MyIniValue value = iniUtil.Get(SECTION_NAME, nameof(configversion));
                        if (value.IsEmpty || value.ToString() != configversion)
                        {
                            ResetSettings();
                            SaveSettings();
                            return;
                        }

                        // Get the values
                        allowvanillagyros = iniUtil.Get(SECTION_NAME, nameof(allowvanillagyros)).ToBoolean();
                        maxpitchangle = (float)iniUtil.Get(SECTION_NAME, nameof(maxpitchangle)).ToDouble();
                        maxrollangle = (float)iniUtil.Get(SECTION_NAME, nameof(maxrollangle)).ToDouble();
                        correctionspeed = (float)iniUtil.Get(SECTION_NAME, nameof(correctionspeed)).ToDouble();
                        minspeedtoturnmps = iniUtil.Get(SECTION_NAME, nameof(minspeedtoturnmps)).ToDouble();
                        maxturnspeed = (float)iniUtil.Get(SECTION_NAME, nameof(maxturnspeed)).ToDouble();
                        minturnspeedmodifier = (float)iniUtil.Get(SECTION_NAME, nameof(minturnspeedmodifier)).ToDouble();
                        maxturnspeedmodifier = (float)iniUtil.Get(SECTION_NAME, nameof(maxturnspeedmodifier)).ToDouble();
                    }
                    else
                    {
                        ResetSettings();
                        ShowParseError();
                    }
                }
                else
                {
                    // Not yet existing so we make one
                    ResetSettings();
                    SaveSettings();
                }
            }
            catch (Exception)
            {
                ResetSettings();
                ShowParseError();
            }
        }

        private void SaveSettings()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(SETTINGS_FILENAME, typeof(ShipRudderSettings)))
            {
                MyAPIGateway.Utilities.DeleteFileInWorldStorage(SETTINGS_FILENAME, typeof(ShipRudderSettings));
            }
            var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(SETTINGS_FILENAME, typeof(ShipRudderSettings));

            iniUtil.Clear();
            iniUtil.AddSection(SECTION_NAME);

            // Set the values
            iniUtil.Set(SECTION_NAME, nameof(configversion), configversion);
            iniUtil.Set(SECTION_NAME, nameof(allowvanillagyros), allowvanillagyros);
            iniUtil.Set(SECTION_NAME, nameof(maxpitchangle), maxpitchangle);
            iniUtil.Set(SECTION_NAME, nameof(maxrollangle), maxrollangle);
            iniUtil.Set(SECTION_NAME, nameof(correctionspeed), correctionspeed);
            iniUtil.Set(SECTION_NAME, nameof(minspeedtoturnmps), minspeedtoturnmps);
            iniUtil.Set(SECTION_NAME, nameof(maxturnspeed), maxturnspeed);
            iniUtil.Set(SECTION_NAME, nameof(minturnspeedmodifier), minturnspeedmodifier);
            iniUtil.Set(SECTION_NAME, nameof(maxturnspeedmodifier), maxturnspeedmodifier);

            writer.Write(iniUtil.ToString());
            writer.Close();
        }

        public string GetAvailableSettings()
        {
            string availableSettings = $"/r{nameof(allowvanillagyros)}, " +
                                       $"/r{nameof(maxpitchangle)}, " +
                                       $"/r{nameof(maxrollangle)}, " +
                                       $"/r{nameof(correctionspeed)}, " +
                                       $"/r{nameof(minspeedtoturnmps)}, " +
                                       $"/r{nameof(maxturnspeed)}, " +
                                       $"/r{nameof(minturnspeedmodifier)}, " +
                                       $"/r{nameof(maxturnspeedmodifier)}";

            return availableSettings;
        }

        public string GetSetting(string name)
        {
            switch (name)
            {
                case nameof(allowvanillagyros): return allowvanillagyros.ToString();
                case nameof(maxpitchangle): return maxpitchangle.ToString();
                case nameof(maxrollangle): return maxrollangle.ToString();
                case nameof(correctionspeed): return correctionspeed.ToString();
                case nameof(minspeedtoturnmps): return minspeedtoturnmps.ToString();
                case nameof(maxturnspeed): return maxturnspeed.ToString();
                case nameof(minturnspeedmodifier): return minturnspeedmodifier.ToString();
                case nameof(maxturnspeedmodifier): return maxturnspeedmodifier.ToString();
                default: return string.Empty;
            }
        }

        public SettingLimits GetLimits(string name)
        {
            return settingLimits[name];
        }

        public bool UpdateSetting(string name, string value)
        {
            bool result = false;
            if (settingLimits.ContainsKey(name))
            {
                SettingLimits limit = settingLimits[name];
                result = limit.IsValid(value);

                if (result)
                {
                    switch (name)
                    {
                        case nameof(allowvanillagyros): allowvanillagyros = bool.Parse(value); break;
                        case nameof(maxpitchangle): maxpitchangle = float.Parse(value); break;
                        case nameof(maxrollangle): maxrollangle = float.Parse(value); break;
                        case nameof(correctionspeed): correctionspeed = float.Parse(value); break;
                        case nameof(minspeedtoturnmps): minspeedtoturnmps = double.Parse(value); break;
                        case nameof(maxturnspeed): maxturnspeed = float.Parse(value); break;
                        case nameof(minturnspeedmodifier): minturnspeedmodifier = float.Parse(value); break;
                        case nameof(maxturnspeedmodifier): maxturnspeedmodifier = float.Parse(value); break;
                        default: return false;
                    }
                }
            }

            return result;
        }

        private void ShowParseError()
        {
            string message = "Error reading Ship Rudder Mod settings, settings have been reset.";
            ShipRudderSession.Instance.Messaging.NotifyPlayer(message);
            MyLog.Default.WriteLineAndConsole(message);
        }

    }

    public abstract class SettingLimits
    {
        public abstract bool IsValid(object value);
        protected bool IsWithinLimits(double value, double min, double max)
        {
            return (value >= min && value <= max);
        }
    }

    public class BoolLimits : SettingLimits
    {
        public bool DefaultValue;

        public override bool IsValid(object value)
        {
            bool boolValue;
            return bool.TryParse(value.ToString(), out boolValue);
        }
    }

    public class FloatLimits : SettingLimits
    {
        public float DefaultValue;
        public float MinValue;
        public float MaxValue;

        public override bool IsValid(object value)
        {
            float floatValue;
            if (float.TryParse(value.ToString(), out floatValue))
            {
                if(IsWithinLimits(floatValue, MinValue, MaxValue))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class DoubleLimits : SettingLimits
    {
        public double DefaultValue;
        public float MinValue;
        public float MaxValue;

        public override bool IsValid(object value)
        {
            double doubleValue;
            if (double.TryParse(value.ToString(), out doubleValue))
            {
                if (IsWithinLimits(doubleValue, MinValue, MaxValue))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
