﻿using CustomUnits;
using HBS.Collections;
using System;

namespace SizeMatters.Helper
{
    public static class StraightTonnageCalculator
    {
        public static string CacheKey(AbstractActor attacker, ICombatant target)
        {
            return $"{attacker.DistinctId()}-{target.DistinctId()}";
        }

        public static int Modifier(AbstractActor attacker, ICombatant target)
        {
            int modifier = 0;

            try
            {
                float attackerTonnage = GetTonnage(attacker);
                float targetTonnage = GetTonnage(target);
                float tonnageDelta = attackerTonnage - targetTonnage;
                Mod.Log.Debug?.Write($"TonnageDelta: {tonnageDelta} from attackerTonnage: {attackerTonnage} - targetTonnage: {targetTonnage}");

                float tonnageFraction = tonnageDelta / Mod.Config.TonnageDivisor;
                Mod.Log.Debug?.Write($"Tonnage fraction: {tonnageFraction} = tonnageDelta: {tonnageDelta} / tonnageDiv: {Mod.Config.TonnageDivisor}");

                // a 20 vs t 100 => 20 - 100 = -80
                // a 100 vs t 20 => 100 - 20 = 80
                // a 50 vs t 50 => 0
                if (tonnageFraction < 0 || tonnageFraction > 0)
                {
                    modifier = (int)Math.Floor(tonnageFraction);
                    Mod.Log.Debug?.Write($"RawMod: {tonnageFraction} => ceiling = {modifier}");
                }
                else
                {
                    modifier = 0;
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn?.Write(e, "Failed to calculate tonnage delta modifier!");
            }

            if (Math.Abs(modifier) > Mod.Config.ModifierCap)
            {
                if (modifier > 0) modifier = Mod.Config.ModifierCap;
                else if (modifier < 0) modifier = Mod.Config.ModifierCap * -1;
            }

            return modifier;
        }

        public static float GetTonnage(ICombatant combatant)
        {
            float tonnage = 0f;
            if (combatant == null)
            {
                Mod.Log.Debug?.Write($"Combatant is null, using tonnage of 0!");
            }
            else if (combatant is BattleTech.Building)
            {
                Mod.Log.Debug?.Write($"Using virtual tonnage: {Mod.Config.VirtualTonnage.Building} for building: {combatant.DistinctId()}");
                return Mod.Config.VirtualTonnage.Building;
            }
            else if (combatant is Turret turret)
            {
                TagSet actorTags = turret.GetTags();
                if (actorTags != null && actorTags.Contains("unit_light"))
                {
                    tonnage = Mod.Config.VirtualTonnage.LightTurret;
                }
                else if (actorTags != null && actorTags.Contains("unit_medium"))
                {
                    tonnage = Mod.Config.VirtualTonnage.MediumTurret;
                }
                else if (actorTags != null && actorTags.Contains("unit_heavy"))
                {
                    tonnage = Mod.Config.VirtualTonnage.HeavyTurret;
                }
                else
                {
                    tonnage = Mod.Config.VirtualTonnage.DefaultTurret;
                }
                Mod.Log.Debug?.Write($"Using virtual tonnage: {tonnage} for turret: {turret.DistinctId()}");                
            }
            else if (combatant is Mech mech)
            {
                UnitCustomInfo customInfo = mech.GetCustomInfo();
                if (customInfo != null && customInfo.SquadInfo != null && customInfo.SquadInfo.Troopers > 1)
                {
                    tonnage = mech.tonnage / customInfo.SquadInfo.Troopers;
                    Mod.Log.Debug?.Write($"Using tonnage: {tonnage} for squad: {mech.DistinctId()} with raw tonnage: {mech.tonnage} and trooper count: {customInfo.SquadInfo.Troopers}");
                }
                else
                {
                    tonnage = mech.tonnage;
                    Mod.Log.Debug?.Write($"Using tonnage: {mech.tonnage} for mech: {mech.DistinctId()}");
                }
            }
            else if (combatant is Vehicle vehicle)
            {
                tonnage = vehicle.tonnage;
                Mod.Log.Debug?.Write($"Using tonnage: {vehicle.tonnage} for vehicle: {vehicle.DistinctId()}");
            }
            
            if (combatant is AbstractActor actor)
            {
                foreach (string tag in actor.GetTags())
                {
                    if (Mod.Config.UnitTonnageRatio.TryGetValue(tag, out float tonnageRatio))
                    {
                        Mod.Log.Debug?.Write($"Applying Unit Tonnage Ratio {tonnageRatio} from tag {tag}");
                        Mod.Log.Debug?.Write($"  New Tonnage is: {tonnage} * {tonnageRatio} = {tonnage * tonnageRatio}");
                        tonnage *= tonnageRatio;
                        break;
                    }
                }
            }

            if (tonnage > Mod.Config.TonnageCapMax)
            {
                Mod.Log.Debug?.Write($"Calculated tonnage: {tonnage} exceeds configured max tonnageCap: {Mod.Config.TonnageCapMax}. Using tonnage cap.");
                return Mod.Config.TonnageCapMax;
            }
            if (tonnage < Mod.Config.TonnageCapMin)
            {
                Mod.Log.Debug?.Write($"Calculated tonnage: {tonnage} exceeds configured min tonnageCap: {Mod.Config.TonnageCapMin}. Using tonnage cap.");
                return Mod.Config.TonnageCapMin;
            }
            return tonnage;
        }
    }
}
