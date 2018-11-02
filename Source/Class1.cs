using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;

namespace EnhancedBattery
{
    public class CompChargeBackBatteryPrototype : CompPowerBattery
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Fill",
                    action = delegate ()
                    {
                        this.SetStoredEnergyPct(1f);
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Empty",
                    action = delegate ()
                    {
                        this.SetStoredEnergyPct(0f);
                    }
                };
            }
            yield break;
        }
    }
    public class CompChargeBackPowerPlantPrototype :CompPowerTrader
    {
        protected CompChargeBackBatteryPrototype compChargeBackBatteryProt;
        protected CompBreakdownable breakdownableComp;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            this.breakdownableComp = this.parent.GetComp<CompBreakdownable>();
            if (base.Props.basePowerConsumption < 0f && !this.parent.IsBrokenDown() && FlickUtility.WantsToBeOn(this.parent))
            {
                base.PowerOn = true;
            }
            if(parent.GetComp<CompChargeBackBatteryPrototype>()!=null)
            {
                compChargeBackBatteryProt = parent.GetComp<CompChargeBackBatteryPrototype>();
            }

        }

        public override void PostExposeData()
        {
            Thing thing = null;
            if (Scribe.mode == LoadSaveMode.Saving && this.connectParent != null)
            {
                thing = this.connectParent.parent;
            }
            //Scribe_References.Look<Thing>(ref thing, "parentThing", false);
            if (thing != null)
            {
                this.connectParent = ((ThingWithComps)thing).GetComp<CompPower>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && this.connectParent != null)
            {
                this.ConnectToTransmitter(this.connectParent, true);
            }
        }
        public override void CompTick()
        {
            base.CompTick();
            this.UpdateDesiredPowerOutput();
        }
        public override void SetUpPowerVars()
        {
            base.SetUpPowerVars();
            CompProperties_Power props = base.Props;
            if (compChargeBackBatteryProt != null)
            {
                if (compChargeBackBatteryProt.StoredEnergyPct > 0.9)
                {
                    PowerOutput = -Props.basePowerConsumption;
                    return;
                }
            }
            PowerOutput = 0;
        }
        public virtual void UpdateDesiredPowerOutput()
        {
            if ((this.breakdownableComp != null && this.breakdownableComp.BrokenDown) || (this.flickableComp != null && !this.flickableComp.SwitchIsOn) || !base.PowerOn)
            {
                base.PowerOutput = 0f;
                return;
            }
            if (compChargeBackBatteryProt != null)
            {
                if (compChargeBackBatteryProt.StoredEnergyPct > 0.9)
                {
                    PowerOutput = -Props.basePowerConsumption;
                    return;
                }
            }
            PowerOutput = 0;
        }

        public override string CompInspectStringExtra()
        {
            string str;
            str = "PowerOutput".Translate() + ": " + this.PowerOutput.ToString("#####0") + " W";
            return str;
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }
    }

    public class CompChargeBackPowerPlant : CompChargeBackPowerPlantPrototype
    {
        public float currentPowerEfficiency = 1.0f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            currentPowerEfficiency = ((CompProperties_ChargeBackPowerPlant)Props).powerEfficiency;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<float>(ref currentPowerEfficiency, "currentPowerEfficiency");
        }

        public override void CompTick()
        {
            base.CompTick();
            int tempEff = (int)(compChargeBackBatteryProt.StoredEnergyPct * 100);
            tempEff /= 10;
            currentPowerEfficiency = ((float)tempEff) / 10+0.1f;
            this.UpdateDesiredPowerOutput();
        }

        public override void UpdateDesiredPowerOutput()
        {
            if ((this.breakdownableComp != null && this.breakdownableComp.BrokenDown) || (this.flickableComp != null && !this.flickableComp.SwitchIsOn) || !base.PowerOn)
            {
                base.PowerOutput = 0f;
                return;
            }
            if (compChargeBackBatteryProt != null)
            {
                PowerOutput = -(Props.basePowerConsumption * currentPowerEfficiency);
                return;
            }
            PowerOutput = 0;
        }

        public override string CompInspectStringExtra()
        {
            string str;
            str = "PowerOutput".Translate() + ": " + this.PowerOutput.ToString("#####0") + " W";
            return str;
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }
    }
    public class CompSpeedChargeBattery : CompPowerBattery
    {
        private float chargeEfficiency = 0;

        public new float AmountCanAccept
        {
            get
            {
                if (this.parent.IsBrokenDown())
                {
                    return 0f;
                }
                CompProperties_Battery props = this.Props;
                return (props.storedEnergyMax - StoredEnergy) / chargeEfficiency;
            }
        }

        public new CompProperties_Battery Props
        {
            get
            {
                return (CompProperties_Battery)this.props;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<float>(ref this.chargeEfficiency, "chargeEfficiency", 0f, false);
            CompProperties_Battery props = this.Props;
            if (StoredEnergy > props.storedEnergyMax)
            {
                SetStoredEnergyPct(1);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            this.DrawPower(Mathf.Min(5f * CompPower.WattsToWattDaysPerTick, StoredEnergy));
            chargeEfficiency = (Props.efficiency - StoredEnergyPct);
        }

        public new void AddEnergy(float amount)
        {
            if (amount < 0f)
            {
                Log.Error("Cannot add negative energy " + amount, false);
                return;
            }
            if (amount > this.AmountCanAccept)
            {
                amount = this.AmountCanAccept;
            }
            amount *= chargeEfficiency;
            SetStoredEnergyPct((StoredEnergy + amount) / Props.storedEnergyMax);
        }
        public override void ReceiveCompSignal(string signal)
        {
            if (signal == "Breakdown")
            {
                this.DrawPower(this.StoredEnergy);
            }
        }

        public override string CompInspectStringExtra()
        {
            CompProperties_Battery props = this.Props;
            string text = string.Concat(new string[]
            {
                "PowerBatteryStored".Translate(),
                ": ",
                StoredEnergy.ToString("F0"),
                " / ",
                props.storedEnergyMax.ToString("F0"),
                " Wd"
            });
            string text2 = text;
            text = string.Concat(new string[]
            {
                text2,
                "\n",
                "PowerBatteryEfficiency".Translate(),
                ": ",
                (chargeEfficiency * 100f).ToString("F0"),
                "%"
            });
            if (StoredEnergy > 0f)
            {
                text2 = text;
                text = string.Concat(new string[]
                {
                    text2,
                    "\n",
                    "SelfDischarging".Translate(),
                    ": ",
                    5f.ToString("F0"),
                    " W"
                });
            }
            string value = (this.PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick).ToString("F0");
            string value2 = this.PowerNet.CurrentStoredEnergy().ToString("F0");
            return text + "\n"+ "PowerConnectedRateStored".Translate(value, value2);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Fill",
                    action = delegate ()
                    {
                        this.SetStoredEnergyPct(1f);
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: Empty",
                    action = delegate ()
                    {
                        this.SetStoredEnergyPct(0f);
                    }
                };
            }
            yield break;
        }
    }

    public class CompProperties_ChargeBackBattery : CompProperties_Battery
    {
        public float chargeEfficiency=0.5f;
    }
    public class CompProperties_ChargeBackPowerPlant : CompProperties_Power
    {
        public float powerEfficiency=1.0f;
    }


    [StaticConstructorOnStartup]
    public class Building_ChargeBackBattery : Building
    {
        private int ticksToExplode;

        private Sustainer wickSustainer;

        private static readonly Vector2 BarSize = new Vector2(1.3f, 0.4f);

        private const float MinEnergyToExplode = 500f;

        private const float EnergyToLoseWhenExplode = 400f;

        private const float ExplodeChancePerDamage = 0.05f;

        private static readonly Material BatteryBarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.9f, 0.85f, 0.2f), false);

        private static readonly Material BatteryBarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f), false);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.ticksToExplode, "ticksToExplode", 0, false);
        }

        public override void Draw()
        {
            base.Draw();
            CompPowerBattery comp = base.GetComp<CompPowerBattery>();
            GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
            r.center = this.DrawPos + Vector3.up * 0.1f;
            r.size = Building_ChargeBackBattery.BarSize;
            r.fillPercent = comp.StoredEnergy / comp.Props.storedEnergyMax;
            r.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.2f, 0.85f, 0.85f), false);
            r.unfilledMat = BatteryBarUnfilledMat;
            r.margin = 0.15f;
            Rot4 rotation = base.Rotation;
            rotation.Rotate(RotationDirection.Clockwise);
            r.rotation = rotation;
            GenDraw.DrawFillableBar(r);
            if (this.ticksToExplode > 0 && base.Spawned)
            {
                base.Map.overlayDrawer.DrawOverlay(this, OverlayTypes.BurningWick);
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (this.ticksToExplode > 0)
            {
                if (this.wickSustainer == null)
                {
                    this.StartWickSustainer();
                }
                else
                {
                    this.wickSustainer.Maintain();
                }
                this.ticksToExplode--;
                if (this.ticksToExplode == 0)
                {
                    IntVec3 randomCell = this.OccupiedRect().RandomCell;
                    float radius = Rand.Range(0.5f, 1f) * 3f;
                    GenExplosion.DoExplosion(randomCell, base.Map, radius, DamageDefOf.Flame, null, -1, -1f, null, null, null, null, null, 0f, 1, false, null, 0f, 1, 0f, false);
                    base.GetComp<CompPowerBattery>().DrawPower(400f);
                }
            }
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (!base.Destroyed && this.ticksToExplode == 0 && dinfo.Def == DamageDefOf.Flame && Rand.Value < 0.05f && base.GetComp<CompPowerBattery>().StoredEnergy > 500f)
            {
                this.ticksToExplode = Rand.Range(70, 150);
                this.StartWickSustainer();
            }
        }

        private void StartWickSustainer()
        {
            SoundInfo info = SoundInfo.InMap(this, MaintenanceType.PerTick);
            this.wickSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
        }
    }
    [StaticConstructorOnStartup]
    public class Building_SpeedChargeBattery : Building
    {
        private int ticksToExplode;

        private Sustainer wickSustainer;

        private static readonly Vector2 BarSize = new Vector2(1.3f, 0.4f);

        private const float MinEnergyToExplode = 500f;

        private const float EnergyToLoseWhenExplode = 400f;

        private const float ExplodeChancePerDamage = 0.05f;

        private static readonly Material BatteryBarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.9f, 0.85f, 0.2f), false);

        private static readonly Material BatteryBarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f), false);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.ticksToExplode, "ticksToExplode", 0, false);
        }

        public override void Draw()
        {
            base.Draw();
            CompSpeedChargeBattery comp = base.GetComp<CompSpeedChargeBattery>();
            GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
            r.center = this.DrawPos + Vector3.up * 0.1f;
            r.size = Building_SpeedChargeBattery.BarSize;
            r.fillPercent = comp.StoredEnergy / comp.Props.storedEnergyMax;
            r.filledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.15f+comp.StoredEnergyPct, 1.5f-comp.StoredEnergyPct, 1.2f - comp.StoredEnergyPct), false);
            r.unfilledMat = BatteryBarUnfilledMat;
            r.margin = 0.15f;
            Rot4 rotation = base.Rotation;
            rotation.Rotate(RotationDirection.Clockwise);
            r.rotation = rotation;
            GenDraw.DrawFillableBar(r);
            if (this.ticksToExplode > 0 && base.Spawned)
            {
                base.Map.overlayDrawer.DrawOverlay(this, OverlayTypes.BurningWick);
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (this.ticksToExplode > 0)
            {
                if (this.wickSustainer == null)
                {
                    this.StartWickSustainer();
                }
                else
                {
                    this.wickSustainer.Maintain();
                }
                this.ticksToExplode--;
                if (this.ticksToExplode == 0)
                {
                    IntVec3 randomCell = this.OccupiedRect().RandomCell;
                    float radius = Rand.Range(0.5f, 1f) * 3f;
                    GenExplosion.DoExplosion(randomCell, base.Map, radius, DamageDefOf.Flame, null, -1, -1f, null, null, null, null, null, 0f, 1, false, null, 0f, 1, 0f, false);
                    base.GetComp<CompPowerBattery>().DrawPower(400f);
                }
            }
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (!base.Destroyed && this.ticksToExplode == 0 && dinfo.Def == DamageDefOf.Flame && Rand.Value < 0.05f && base.GetComp<CompPowerBattery>().StoredEnergy > 500f)
            {
                this.ticksToExplode = Rand.Range(70, 150);
                this.StartWickSustainer();
            }
        }

        private void StartWickSustainer()
        {
            SoundInfo info = SoundInfo.InMap(this, MaintenanceType.PerTick);
            this.wickSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
        }
    }
}
