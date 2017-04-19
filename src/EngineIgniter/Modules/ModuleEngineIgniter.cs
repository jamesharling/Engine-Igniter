using System;
using UnityEngine;

namespace EngineIgniter.Modules
{
    [KSPModule("Engine Igniter")]
    public class ModuleEngineIgniter : PartModule
    {
        [KSPField(isPersistant = false)]
        public int IgnitionsPermitted;

        [KSPField(isPersistant = true, guiName = "Ignitions Available", guiActive = true, guiActiveEditor = true)]
        public int IgnitionsAvailable;

        private ModuleEngines engine;

        private EngineIgnitionState engineState = EngineIgnitionState.NotIgnited;

        private StartState startState = StartState.None;

        private enum EngineIgnitionState
        {
            NotIgnited,
            Ignited
        }

        private bool IsStartable => !this.RequiresIgnition || this.IgnitionsAvailable > 0;

        private bool RequiresIgnition => this.IgnitionsPermitted > 0;

        public override string GetInfo()
        {
            string info = "This engine can be started ";

            if (this.RequiresIgnition)
            {
                info += $"{this.IgnitionsPermitted} {this.Pluralise("time", this.IgnitionsPermitted)}.";
            }
            else
            {
                info += "an unlimited number of times.";
            }

            return info;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            string info = $"Loaded on {this.part.name}; ";

            if (this.RequiresIgnition)
            {
                info += $"{this.IgnitionsPermitted} ignitions permitted, {this.IgnitionsAvailable} ignitions remaining.";
            }
            else
            {
                info += "infinite ignitions available.";
            }

            this.Log(info);
        }

        public override void OnStart(StartState state)
        {
            this.startState = state;

            this.engine = this.part.Modules.GetModule<ModuleEngines>();

            // If we are in the editor, set the available ignitions to max
            if (this.startState == StartState.Editor)
            {
                this.IgnitionsAvailable = this.IgnitionsPermitted;
            }
        }

        public override void OnUpdate()
        {
            // Don't do anything if we are in the editor
            if (this.startState == StartState.None || this.startState == StartState.Editor)
            {
                return;
            }

            // If the engine was previously not ignited, is in a start-able state AND the throttle is
            // being operated, we can count this as an attempt at ignition of the engine
            if (this.engineState == EngineIgnitionState.NotIgnited && this.engine.requestedThrottle > 0.0f)
            {
                // If the engine is in a start-able state, use up one ignition count and switch the
                // engine's state
                if (this.IsStartable)
                {
                    if (this.IgnitionsAvailable > 0)
                    {
                        this.IgnitionsAvailable--;
                    }

                    this.Log($"Igniting {this.engine.name}: {this.IgnitionsAvailable} ignitions remaining");

                    this.engineState = EngineIgnitionState.Ignited;
                }

                // If not, we forcibly shutdown the engine
                else
                {
                    this.Log($"{this.engine.name} has no ignitions remaining");

                    // Let's be nice and tell the pilot what's going on...
                    ScreenMessages.PostScreenMessage($"{this.engine.part.partInfo.title} has no ignitions remaining!", 5.0f);

                    this.ShutdownEngine();
                }
            }

            // Conversely, if the engine previously was ignited and the throttle has since dropped to
            // 0, the engine is considered to be shutdown
            else if (this.engineState == EngineIgnitionState.Ignited && this.engine.requestedThrottle <= 0.0f)
            {
                this.engineState = EngineIgnitionState.NotIgnited;
            }
        }

        private void Log(string message)
        {
            Debug.Log($"[ModuleEngineIgnitor]: {message}");
        }

        private string Pluralise(string noun, int count) => noun + (count != 1 ? "s" : String.Empty);

        private void ShutdownEngine()
        {
            this.engine.SetRunningGroupsActive(false);

            foreach (var e in this.engine.Events)
            {
                if (e.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    this.Log($"Shutting down engine {this.engine.name}");

                    e.Invoke();
                }
            }

            this.engine.SetRunningGroupsActive(false);
        }
    }
}