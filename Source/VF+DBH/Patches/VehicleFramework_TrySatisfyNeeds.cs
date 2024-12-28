using DubsBadHygiene;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VF_DBH.Patches
{
    [HarmonyPatch(typeof(VehiclePawn))]
    [HarmonyPatch("TrySatisfyPawnNeeds")]
    [HarmonyPatch(new Type[] { typeof(Pawn) })]
    public static class VehiclePawn_TrySatisfyPawnNeeds_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, VehiclePawn __instance)
        {
            if (pawn.Dead) return;

            List<Need> allNeeds = pawn.needs.AllNeeds;
            VehicleHandler handler = pawn.ParentHolder as VehicleHandler;
            int tile;
            VehicleCaravan vehicleCaravan = pawn.GetVehicleCaravan();
            if (vehicleCaravan != null)
            {
                tile = vehicleCaravan.Tile;
            }
            else if (handler != null)
            {
                tile = handler.vehicle.Map.Tile;
            }
            else if (pawn.Spawned)
            {
                tile = pawn.Map.Tile;
            }
            else
            {
                //Log.Error($"Trying to satisfy pawn needs but pawn is not part of VehicleCaravan, vehicle crew, or spawned.");
                return;
            }

            Need_Hygiene need_Hygiene = pawn.needs.TryGetNeed<Need_Hygiene>();
            Need_Thirst need_Thirst = pawn.needs.TryGetNeed<Need_Thirst>();
            Need_Bladder need_Bladder = pawn.needs.TryGetNeed<Need_Bladder>();

            // Don't shit in the tank mid raid...
            if (need_Bladder != null && vehicleCaravan != null)
            {
                need_Bladder.CurLevel = 1f;
            }

            // Wash only in caravan mode, if at least halfway between Fine and Grimy.
            if (need_Hygiene != null && need_Hygiene.CurLevel <= 0.4f && vehicleCaravan != null)
            {
                bool num = !Find.WorldGrid[pawn.Tile].Rivers.NullOrEmpty();
                float chance = GenMath.LerpDoubleClamped(0f, 1200f, 0f, 1f, Find.WorldGrid[pawn.Tile].rainfall);
                // Bathe in river / shower with rain only in caravan mode.
                if (num || Rand.Chance(chance))
                {
                    need_Hygiene.CurLevel = 0.7f;
                }
                else
                {
                    Thing innerIfMinified = GetThingWithOwner(pawn, (Thing x) => x.GetInnerIfMinified() is Building_Butt building_Butt && building_Butt.GetComp<CompWaterStorage>().WaterStorage > 0f, out Pawn owner1).GetInnerIfMinified();
                    if (innerIfMinified != null)
                    {
                        innerIfMinified.TryGetComp<CompWaterStorage>().WaterStorage -= 1f;
                        need_Hygiene.CurLevel = 0.7f;
                    }
                }
            }

            // Have a sip of water even if just sitting in the car.
            if (need_Thirst == null || (int)need_Thirst.CurCategory > 1)
            {
                return;
            }

            Thing innerIfMinified2 = GetThingWithOwner(pawn, (Thing x) => x.GetInnerIfMinified() is Building_Butt building_Butt && building_Butt.GetComp<CompWaterStorage>().WaterStorage > 0f, out Pawn owner2).GetInnerIfMinified();

            if (innerIfMinified2 != null)
            {
                CompWaterStorage compWaterStorage = innerIfMinified2.TryGetComp<CompWaterStorage>();
                compWaterStorage.WaterStorage -= 0.5f;
                need_Thirst.CurLevel = 1f;
                SanitationUtil.ContaminationCheckWater(pawn, compWaterStorage.WaterQuality);
                return;
            }

            Thing thing = GetThingWithOwner(pawn, (Thing x) => val(x.def), out Pawn ownerOf);
            if (thing != null)
            {
                thing.Ingested(pawn, pawn.needs.food.NutritionWanted);
                if (thing.Destroyed && ownerOf != null)
                {
                    ownerOf.inventory.innerContainer.Remove(thing);
                    if (vehicleCaravan != null)
                    {
                        vehicleCaravan.RecacheImmobilizedNow();
                        vehicleCaravan.RecacheDaysWorthOfFood();
                    }
                }
            }
            // Drink from river / rain water only when in caravan mode.
            else if (vehicleCaravan != null)
            {
                bool num2 = !Find.WorldGrid[pawn.Tile].Rivers.NullOrEmpty();
                float chance2 = GenMath.LerpDoubleClamped(0f, 2000f, 0f, 1f, Find.WorldGrid[pawn.Tile].rainfall);
                if (num2 || Rand.Chance(chance2))
                {
                    need_Thirst.CurLevel = 1f;
                    SanitationUtil.ContaminationCheckWater(pawn, ContaminationLevel.Untreated);
                }
            }
        }

        static bool val(ThingDef x)
        {
            return x.GetModExtension<WaterExt>()?.SeekForThirst ?? false;
        }

        public static Thing GetThingWithOwner(Pawn forPawn, Func<Thing, bool> filter, out Pawn owner)
        {
            Thing foundThing = null;
            owner = null;

            if (forPawn.GetVehicleCaravan() is VehicleCaravan vehicleCaravan)
            {
                foundThing = CaravanInventoryUtility.AllInventoryItems(vehicleCaravan).FirstOrDefault(filter);
                if (foundThing != null)
                {
                    owner = CaravanInventoryUtility.GetOwnerOf(vehicleCaravan, foundThing);
                }
            } else if (forPawn.ParentHolder is VehicleHandler handler)
            {
                owner = forPawn;
                foundThing = forPawn.inventory.innerContainer.FirstOrDefault(filter);
                if (foundThing is null)
                {
                    VehiclePawn vehicle = handler.vehicle;
                    owner = vehicle;
                    foundThing = vehicle.inventory.innerContainer.FirstOrDefault(filter);
                }
            }

            return foundThing;
        }
    }
}
