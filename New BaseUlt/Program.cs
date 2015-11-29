﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;
using Font = EloBuddy.SDK.Rendering.Text;
using Sprite = EloBuddy.SDK.Rendering.Sprite;
using New_BaseUlt.Properties;


namespace New_BaseUlt
{
    public class BaseUltMenu
    {
        public static void Main(string[] args)
        {
            Loading.OnLoadingComplete += Loading_OnLoadingComplete; //Start at Game Start
        }
        public static void Loading_OnLoadingComplete(EventArgs args)
        {
            if (ObjectManager.Player.BaseSkinName == ("Ashe"))
                Init.LoadMenu();
            else
                Init.LoadMenu();
        }
    }
    internal class Init
    {
        public static Menu Menu;

        public static void LoadMenu() //Menu
        {
            Bootstrap.Init(null);

            Menu = MainMenu.AddMenu("BaseUlt", "Baseult");
            Menu.AddGroupLabel("BaseUlt");
            Menu.AddLabel("Test");
            Menu.AddSeparator();

            Menu.Add("baseult", new CheckBox("Enable BaseUlt?"));
            Menu.Add("showrecall", new CheckBox("Show Recalls?"));
            Menu.Add("showallies", new CheckBox("Show Allies?"));
            Menu.Add("showenemies", new CheckBox("Show Enemies?"));
            Menu.Add("checkcollision", new CheckBox("Check Collision?"));
            Menu.AddSeparator();
            Menu.Add("timelimit", new Slider("Select FOW Time Limit (SEC)", 20, 0, 120));
            Menu.AddSeparator();
            Menu.Add("nobaseult", new KeyBind("No BaseUlt while", false, KeyBind.BindTypes.HoldActive, 32));
            Menu.AddSeparator();
            Menu.Add("x", new Slider("Offset X", 0, -500, 500));
            Menu.Add("y", new Slider("Offset Y", 0, -500, 500));
            Menu.AddGroupLabel("BaseUlt Targets");
            foreach (var unit in EntityManager.Heroes.Enemies)
            {
                Menu.Add("target" + unit.ChampionName, new CheckBox(string.Format("{0} ({1})", unit.ChampionName, unit.Name)));
            }
            Core.Initialize(); //Referenz to Core-Start
            Game.OnUpdate += args1 => Core.OnUpdate(); //Updating Core
            Drawing.OnEndScene += args1 => Core.OnEndScene(); //Updating Core at End of Game
            //Teleport.OnTeleport += Core.OnTeleport; //Updating Core at Teleport BUGGY
        }
    }

    class Core
    {
        private static readonly List<Recall> Recalls = new List<Recall>();
        private static readonly List<BaseUltUnit> BaseUltUnits = new List<BaseUltUnit>();
        private static readonly List<BaseUltSpell> BaseUltSpells = new List<BaseUltSpell>();
        private static readonly AIHeroClient Player = ObjectManager.Player;
        private static Font Text;
        private static Sprite Hud;
        private static Sprite Bar;
        private static Sprite Warning;
        private static Sprite Underline;
        private static int X = (int)TacticalMap.X - 20;
        private static int Y = (int)TacticalMap.Y - 200;
        private const int Length = 260;
        private const int Height = 25;
        private const int LineThickness = 4;

        

        public static void Initialize()
        {
            Text = new Font("",new FontDescription
            {
                FaceName = "Calibri",
                Height = (Height / 30) * 23,
                OutputPrecision = FontPrecision.Default,
                Quality = FontQuality.ClearType
            });

            Init.Menu["x"].Cast<Slider>().OnValueChange += OffsetOnOnValueChange;
            Init.Menu["y"].Cast<Slider>().OnValueChange += OffsetOnOnValueChange;
            UpdateOffset(Init.Menu["x"].Cast<Slider>().CurrentValue, Init.Menu["y"].Cast<Slider>().CurrentValue);

            Hud = new Sprite(Texture.FromMemory(
                Drawing.Direct3DDevice, (byte[])new ImageConverter().ConvertTo(Resources.baseulthud, typeof(byte[])),
                285,
                44, 0, Usage.None, Format.A1, Pool.Managed, Filter.Default, Filter.Default, 0));

            Bar = new Sprite(Texture.FromMemory(
            Drawing.Direct3DDevice, (byte[])new ImageConverter().ConvertTo(Resources.bar, typeof(byte[])), 260,
            66, 0, Usage.None, Format.A1, Pool.Managed, Filter.Default, Filter.Default, 0));

            Warning = new Sprite(Texture.FromMemory(
            Drawing.Direct3DDevice, (byte[])new ImageConverter().ConvertTo(Resources.warning, typeof(byte[])), 40,
            40, 0, Usage.None, Format.A1, Pool.Managed, Filter.Default, Filter.Default, 0));

            Underline = new Sprite(Texture.FromMemory(
            Drawing.Direct3DDevice,
            (byte[])new ImageConverter().ConvertTo(Resources.underline_red, typeof(byte[])), 355,
                89, 0, Usage.None, Format.A1, Pool.Managed, Filter.Default, Filter.Default, 0));
            foreach (var hero in ObjectManager.Get<AIHeroClient>())
            {
                Recalls.Add(new Recall(hero, RecallStatus.Inactive));
            }
            {
                BaseUltSpells.Add(new BaseUltSpell("Ezreal", SpellSlot.R, 1000, 2000, 160, false));
                BaseUltSpells.Add(new BaseUltSpell("Jinx", SpellSlot.R, 600, 1700, 140, true));
                BaseUltSpells.Add(new BaseUltSpell("Ashe", SpellSlot.R, 250, 1600, 130, true));
                BaseUltSpells.Add(new BaseUltSpell("Draven", SpellSlot.R, 400, 2000, 160, true));
                BaseUltSpells.Add(new BaseUltSpell("Karthus", SpellSlot.R, 3125, 0, 0, false));
                BaseUltSpells.Add(new BaseUltSpell("Ziggs", SpellSlot.Q, 250, 3100, 0, false));
                BaseUltSpells.Add(new BaseUltSpell("Lux", SpellSlot.R, 1375, 0, 0, false));
                BaseUltSpells.Add(new BaseUltSpell("Xerath", SpellSlot.R, 700, 600, 0, false));
            }

        }
        private static void OffsetOnOnValueChange(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
        {
            if (sender.SerializationId == Init.Menu["x"].Cast<Slider>().SerializationId)
            {
                UpdateOffset(args.NewValue);
            }
            else
            {
                UpdateOffset(0, args.NewValue);
            }
        }
        private static void UpdateOffset(int x, int y = 0)
        {
            X = (int)TacticalMap.X + (-20 + x);
            Y = (int)TacticalMap.Y + (-200 + y);
        }
        public static void OnUpdate()
        {
            foreach (var recall in Recalls)
            {
                if (recall.Status != RecallStatus.Inactive)
                {
                    var recallDuration = recall.Duration;
                    var cd = recall.Started + recallDuration - Game.Time;
                    var percent = (cd > 0 && Math.Abs(recallDuration) > float.Epsilon)
                        ? 1f - (cd / recallDuration)
                        : 1f;
                    var textLength = (recall.Unit.ChampionName.Length + 6) * 7;
                    var myLength = percent * Length;
                    var freeSpaceLength = myLength + textLength;
                    var freeSpacePercent = freeSpaceLength / Length > 1 ? 1 : freeSpaceLength / Length;

                    if (
                        Recalls.Any(
                            h =>
                                GetRecallPercent(h) > percent && GetRecallPercent(h) < freeSpacePercent &&
                                h.TextPos == recall.TextPos && recall.Started > h.Started))
                    {
                        recall.TextPos += 1;
                    }
                    if (recall.Status == RecallStatus.Finished &&
                        Recalls.Any(
                            h =>
                                h.Started > recall.Started && h.TextPos == recall.TextPos &&
                                recall.Started + 3 < h.Started + recall.Duration))
                    {
                        recall.TextPos += 1;
                    }
                }

                if (recall.Status == RecallStatus.Active)
                {
                    var compatibleChamps = new[] { "Jinx", "Ezreal", "Ashe", "Draven", "Karthus" }; //Ziggs, Xerath, Lux
                    if (recall.Unit.IsEnemy && compatibleChamps.Any(h => h == Player.ChampionName) &&
                        !BaseUltUnits.Any(h => h.Unit.NetworkId == recall.Unit.NetworkId))
                    {
                        var spell = BaseUltSpells.Find(h => h.Name == Player.ChampionName);
                        if (Player.Spellbook.GetSpell(spell.Slot).IsReady &&
                            Player.Spellbook.GetSpell(spell.Slot).Level > 0)
                        {
                            BaseUltCalcs(recall);
                        }
                    }
                }
                if (recall.Status != RecallStatus.Active)
                {
                    var baseultUnit = BaseUltUnits.Find(h => h.Unit.NetworkId == recall.Unit.NetworkId);
                    if (baseultUnit != null)
                    {
                        BaseUltUnits.Remove(baseultUnit);
                    }
                }
            }
            foreach (var unit in BaseUltUnits)
            {
                if (Init.Menu["checkcollision"].Cast<CheckBox>().CurrentValue && unit.Collision)
                {
                    continue;
                }

                if (unit.Unit.IsVisible)
                {
                    unit.LastSeen = Game.Time;
                }
                var timeLimit = Init.Menu["timeLimit"].Cast<Slider>().CurrentValue;
                if (Math.Round(unit.FireTime, 2) == Math.Round(Game.Time, 2) && Game.Time - timeLimit >= unit.LastSeen)
                {
                    var spell = Player.Spellbook.GetSpell(BaseUltSpells.Find(h => h.Name == Player.ChampionName).Slot);
                    if (spell.IsReady)
                    {
                        Player.Spellbook.CastSpell(spell.Slot, GetFountainPos());
                    }
                }
            }
        }
        public static void OnEndScene()
        {
            if (!Init.Menu["showrecalls"].Cast<CheckBox>().CurrentValue && !BaseUltUnits.Any())
            {
                return;
            }

            {
                Drawing.DrawLine(X, Y, X + Length, Y, Height, ColorTranslator.FromHtml("#080d0a"));
            }

            if (BaseUltUnits.Any())
            {
                Warning.Draw(new Vector2(Player.HPBarPosition.X + 40, Player.HPBarPosition.Y - 40));
                Underline.Draw(new Vector2(X - 50, Y + 30));
            }

            foreach (var recall in Recalls.OrderBy(h => h.Started))
            {
                if ((recall.Unit.IsAlly && !Init.Menu["showallies"].Cast<CheckBox>().CurrentValue) ||
                    (recall.Unit.IsAlly && !Init.Menu["showenemies"].Cast<CheckBox>().CurrentValue))
                {
                    continue;
                }

                var recallDuration = recall.Duration;

                if (recall.Status == RecallStatus.Active)
                {
                    var isBaseUlt = BaseUltUnits.Any(h => h.Unit.NetworkId == recall.Unit.NetworkId);
                    var percent = GetRecallPercent(recall);
                    var colorIndicator = isBaseUlt
                        ? Color.OrangeRed
                        : (recall.Unit.IsAlly
                            ? Color.DeepSkyBlue
                            : Color.DarkViolet);
                    var colorText = isBaseUlt
                        ? Color.OrangeRed
                        : (recall.Unit.IsAlly
                            ? Color.DeepSkyBlue
                            : Color.PaleVioletRed);
                    var colorBar = isBaseUlt
                        ? Color.Red
                        : (recall.Unit.IsAlly
                            ? Color.DodgerBlue
                            : Color.MediumVioletRed);

                    Bar.Color = colorBar;
                    Bar.Rectangle = new SharpDX.Rectangle(0, 0,
                        (int)(260 * percent), 22);
                    Bar.Draw(new Vector2(X, Y + 4));

                    Drawing.DrawLine(
                        (int)(percent * Length) + X - (float)(LineThickness * 0.5) + 4,
                        Y - (float)(Height * 0.5),
                        (int)(percent * Length) + X - (float)(LineThickness * 0.5) + 4,
                        Y + (float)(Height * 0.5) + recall.TextPos * 20,
                        LineThickness,
                        colorIndicator);

                    Text.Color = colorText;
                    Text.TextValue = "(" + (int)(percent * 100) + "%) " + recall.Unit.ChampionName;
                    Text.Position = new Vector2((int)(percent * Length) + X - LineThickness, Y + (int)(Height * 0.5 + 20 + LineThickness) + recall.TextPos * 20);
                    Text.Draw();
                }

                if (recall.Status == RecallStatus.Abort || recall.Status == RecallStatus.Finished)
                {
                    const int fadeoutTime = 0;
                    var colorIndicator = recall.Status == RecallStatus.Abort
                        ? Color.OrangeRed
                        : Color.GreenYellow;
                    var colorText = recall.Status == RecallStatus.Abort
                        ? Color.Orange
                        : Color.GreenYellow;
                    var colorBar = recall.Status == RecallStatus.Abort
                        ? Color.Yellow
                        : Color.LawnGreen;

                    var fadeOutPercent = (recall.Ended + fadeoutTime - Game.Time) / fadeoutTime;

                    if (recall.Ended + fadeoutTime > Game.Time)
                    {
                        var timeUsed = recall.Ended - recall.Started;
                        var percent = timeUsed > recallDuration ? 1 : timeUsed / recallDuration;

                        Bar.Color = colorBar;
                        Bar.Rectangle = new SharpDX.Rectangle(0, 0, (int)(260 * percent), 22);
                        Bar.Draw(new Vector2(X, Y + 4));

                        Drawing.DrawLine(
                            (int)(percent * Length) + X - (float)(LineThickness * 0.5) + 4,
                            Y - (float)(Height * 0.5),
                            (int)(percent * Length) + X - (float)(LineThickness * 0.5) + 4,
                            Y + (float)(Height * 0.5), LineThickness,
                            Color.FromArgb((int)(254 * fadeOutPercent),
                                colorIndicator));

                        Text.Color = colorText;
                        Text.TextValue = recall.Unit.ChampionName;
                        Text.Position = new Vector2((int)(percent * Length) + X - LineThickness,
                            Y + (int)(Height * 0.5 + 20 + LineThickness) + recall.TextPos * 20);
                        Text.Draw();
                    }
                    else
                    {
                        recall.Status = RecallStatus.Inactive;
                        recall.TextPos = 0;
                    }
                }
            }
            foreach (var unit in BaseUltUnits)
            {
                var duration = Recalls.Find(h => h.Unit.NetworkId == unit.Unit.NetworkId).Duration;
                var barPos =
                    (unit.FireTime - Recalls.Find(h => unit.Unit.NetworkId == h.Unit.NetworkId).Started)
                    / duration;

                Drawing.DrawLine(
                    (int)(barPos * Length) + X - (float)(LineThickness * 0.5),
                    Y - (float)(Height * 0.5 + LineThickness),
                    (int)(barPos * Length) + X - (float)(LineThickness * 0.5),
                    Y + (float)(Height * 0.5 + LineThickness),
                    LineThickness,
                    Color.Lime);
            }
            Hud.Draw(new Vector2(X - 8, Y - 5));
        }
        private static Vector3 GetFountainPos()
        {
            switch (Game.MapId)
            {
                case GameMapId.SummonersRift:
                    {
                        return Player.Team == GameObjectTeam.Order
                            ? new Vector3(14340, 14390, 172)
                            : new Vector3(363, 426, 182);
                    }
                case GameMapId.CrystalScar:
                    {
                        return Player.Team == GameObjectTeam.Order
                            ? new Vector3(524, 4164, 35)
                            : new Vector3(13323, 4105, 36);
                    }
                case GameMapId.TwistedTreeline:
                    {
                        return Player.Team == GameObjectTeam.Order
                            ? new Vector3(1060, 7297, 150)
                            : new Vector3(14353, 7297, 150);
                    }
            }

            return new Vector3();
        }
        private static double GetRecallPercent(Recall recall)
        {
            var recallDuration = recall.Duration;
            var cd = recall.Started + recallDuration - Game.Time;
            var percent = (cd > 0 && Math.Abs(recallDuration) > float.Epsilon)
                ? 1f - (cd / recallDuration)
                : 1f;
            return percent;
        }
        private static float GetBaseUltTravelTime(float delay, float speed)
        {
            if (Player.ChampionName == "Karthus")
            {
                return delay / 1000;
            }

            var distance = Vector3.Distance(Player.ServerPosition, GetFountainPos());
            var missilespeed = speed;

            if (Player.ChampionName == "Jinx" && distance > 1350)
            {
                const float accelerationrate = 0.3f;

                var acceldifference = distance - 1350f;

                if (acceldifference > 150f)
                {
                    acceldifference = 150f;
                }

                var difference = distance - 1500f;

                missilespeed = (1350f * speed + acceldifference * (speed + accelerationrate * acceldifference) + difference * 2200f) / distance;
            }

            return (distance / missilespeed + ((delay - 65) / 1000));
        }
        private static double GetBaseUltSpellDamage(BaseUltSpell spell, AIHeroClient target)
        {
            var level = Player.Spellbook.GetSpell(spell.Slot).Level - 1;
            switch (spell.Name)
            {
                case "Jinx":
                    {
                        float damage = new float[] { 250, 350, 450 }[level] +
                                     new float[] { 25, 30, 35 }[level] / 100 * (target.MaxHealth - target.Health) +
                                     1 * Player.FlatPhysicalDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Physical, damage);
                    }
                case "Ezreal":
                    {
                        float damage = new float[] { 350, 500, 650 }[level] + 0.9f * Player.FlatMagicDamageMod + 1 * Player.FlatPhysicalDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Magical, damage) * 0.7;
                    }
                case "Ashe":
                    {
                        float damage = new float[] { 250, 425, 600 }[level] + 1 * Player.FlatMagicDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Magical, damage);
                    }
                case "Draven":
                    {
                        float damage = new float[] { 175, 275, 375 }[level] + 1.1f * Player.FlatPhysicalDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Physical, damage) * 0.7;
                    }
                case "Karthus":
                    {
                        float damage = new float[] { 250, 400, 550 }[level] + 0.6f * Player.FlatMagicDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Magical, damage); ;
                    }
                case "Lux":
                    {
                        float damage = new float[] { 300, 400, 500 }[level] + 0.75f * Player.FlatMagicDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Magical, damage);
                    }
                case "Xerath":
                    {
                        float damage = new float[] { 190, 245, 300 }[level] + 0.43f * Player.FlatMagicDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Magical, damage);
                    }
                case "Ziggs":
                    {
                        float damage = new float[] { 250, 375, 500 }[level] + 0.9f * Player.FlatMagicDamageMod;
                        return Damage.CalculateDamageOnUnit(Player, target, DamageType.Magical, damage);
                    }
            }

            return 0;
        }
        private static void BaseUltCalcs(Recall recall)
        {
            var finishedRecall = recall.Started + recall.Duration;
            var spellData = BaseUltSpells.Find(h => h.Name == Player.ChampionName);
            var timeNeeded = GetBaseUltTravelTime(spellData.Delay, spellData.Speed);
            var fireTime = finishedRecall - timeNeeded;
            var spellDmg = GetBaseUltSpellDamage(spellData, recall.Unit);
            var collision = GetCollision(spellData.Radius, spellData).Any();
            if (fireTime > Game.Time && fireTime < recall.Started + recall.Duration &&
                recall.Unit.Health < spellDmg &&
                Init.Menu["target" + recall.Unit.ChampionName].Cast<CheckBox>().CurrentValue &&
                Init.Menu["baseult"].Cast<CheckBox>().CurrentValue && !Init.Menu["nobaseult"].Cast<KeyBind>().CurrentValue)
            {
                BaseUltUnits.Add(new BaseUltUnit(recall.Unit, fireTime, collision));
            }
            else if (BaseUltUnits.Any(h => h.Unit.NetworkId == recall.Unit.NetworkId))
            {
                BaseUltUnits.Remove(BaseUltUnits.Find(h => h.Unit.NetworkId == recall.Unit.NetworkId));
            }
        }
        private static List<Obj_AI_Base> GetCollision(float spellwidth, BaseUltSpell spell)
        {
            var collisionList = new List<Obj_AI_Base>();
            foreach (var unit in EntityManager.Heroes.Enemies.Where(h => Player.Distance(h) < 2000))
            {
                var pred = Prediction.Position.PredictLinearMissile(unit, 2000, (int)spell.Radius, (int)spell.Delay,
                    spell.Speed, -1);
                var endpos = Player.ServerPosition.Extend(GetFountainPos(), 2000);
                var projectOn = pred.UnitPosition.To2D()
                    .ProjectOn(Player.ServerPosition.To2D(), endpos);
                if (projectOn.SegmentPoint.Distance(endpos) < spellwidth + unit.BoundingRadius)
                {
                    collisionList.Add(unit);
                }
            }

            return collisionList;
        }
        /*public static void OnTeleport(Obj_AI_Base sender, Teleport.TeleportEventArgs args)
        {
            var unit = Recalls.Find(h => h.Unit.NetworkId == sender.NetworkId);

            if (unit == null || args.Type != TeleportType.Recall)
            {
                return;
            }

            switch (args.Status)
            {
                case TeleportStatus.Start:
                    {
                        unit.Status = RecallStatus.Active;
                        unit.Started = Game.Time;
                        unit.TextPos = 0;
                        unit.Duration = (float)args.Duration / 1000;
                        break;
                    }

                case TeleportStatus.Abort:
                    {
                        unit.Status = RecallStatus.Abort;
                        unit.Ended = Game.Time;
                        break;
                    }

                case TeleportStatus.Finish:
                    {
                        unit.Status = RecallStatus.Finished;
                        unit.Ended = Game.Time;
                        break;
                    }
            }
        }*/
    }
    public class BaseUltUnit //Definition
    {
        public BaseUltUnit(AIHeroClient unit, float fireTime, bool collision)
        {
            Unit = unit;
            FireTime = fireTime;
            Collision = collision;
        }

        public AIHeroClient Unit { get; set; }
        public float FireTime { get; set; }
        public bool Collision { get; set; }
        public float LastSeen { get; set; }
    }
    public class Recall //Definition
    {
        public Recall(AIHeroClient unit, RecallStatus status)
        {
            Unit = unit;
            Status = status;
        }

        public AIHeroClient Unit { get; set; }
        public RecallStatus Status { get; set; }
        public float Started { get; set; }
        public float Ended { get; set; }
        public int TextPos;
        public float Duration { get; set; }
    }
    public class BaseUltSpell //Definition
    {
        public BaseUltSpell(string name, SpellSlot slot, float delay, float speed, float radius, bool collision)
        {
            Name = name;
            Slot = slot;
            Delay = delay;
            Speed = speed;
            Radius = radius;
            Collision = collision;
        }

        public string Name { get; set; }
        public SpellSlot Slot { get; set; }
        public float Delay { get; set; }
        public float Speed { get; set; }
        public float Radius { get; set; }
        public bool Collision { get; set; }
    }
    public enum RecallStatus //Definition
    {
        Active,
        Inactive,
        Finished,
        Abort
    }
}
   


        
