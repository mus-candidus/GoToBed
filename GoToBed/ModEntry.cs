using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace GoToBed {
    public class ModEntry : Mod {
        private Vector2 bed_;

        public override void Entry(IModHelper helper) {
            this.Helper.Events.GameLoop.DayStarted += OnDayStarted;
            this.Helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e) {
            // Every day starts in bed...
            bed_ = Utility.PointToVector2(Utility.getHomeOfFarmer(Game1.player).getBedSpot()) * 64f;
            bed_.X -= 64f;
            bed_.Y += 32f;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e) {
            Game1.player.Position = bed_;
            Game1.player.FacingDirection = 1;
            Game1.player.Halt();
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e) {
            // Intercept sleep dialogue as suggested by Pathos.
            if (e.NewMenu is DialogueBox dialogue) {
                string text = this.Helper.Reflection.GetField<List<string>>(dialogue, "dialogues").GetValue().FirstOrDefault();
                string sleepText = Game1.content.LoadString("Strings\\StringsFromCSFiles:NPC.cs.3996");
                if (text == sleepText) {
                    // handle "Go to sleep for the night?" dialogue
                    this.Monitor.Log("Go to bed?", LogLevel.Debug);
                    Game1.player.currentLocation.afterQuestion = GoToBed;
                }
            }
        }

        private void GoToBed(Farmer who, string whichAnswer) {
            if (whichAnswer.Equals("Yes")) {
                this.Monitor.Log($"Farmer {who.Name} goes to bed", LogLevel.Debug);

                // Player is not married or spouse is in bed already.
                if (!Game1.player.isMarried() || Game1.timeOfDay > 2200) {
                    FarmerSleep();

                    return;
                }

                // Disable player movement so spouse can finish his/her path to bed.
                // TODO: There has to be a better way than resetting the position on every tick!
                // Attach event handler. We need the fast UpdateTicked event to stop player movement!
                this.Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

                NPC spouse = Game1.player.getSpouse();
                FarmHouse farmHouse = who.currentLocation as FarmHouse;

                // If spouse isn't in the farm house player has to sleep alone.
                if (spouse.currentLocation != farmHouse) {
                    this.Monitor.Log($"Spouse {spouse.Name} isn't in the farm house", LogLevel.Info);

                    FarmerSleep();

                    return;
                }

                // Spouse goes to bed.
                this.Monitor.Log($"Spouse {spouse.Name} goes to bed", LogLevel.Debug);

                spouse.controller =
                    new PathFindController(
                        spouse,
                        farmHouse,
                        farmHouse.getSpouseBedSpot(spouse.Name),
                        0,
                        (c, location) => {
                            FarmHouse.spouseSleepEndFunction(c, location);
                            // Detach event handler.
                            this.Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                            // Player can rest assured.
                            FarmerSleep();
                        });

                if (spouse.controller.pathToEndPoint == null) {
                    this.Monitor.Log($"Spouse {spouse.Name} can't reach bed", LogLevel.Warn);

                    FarmerSleep();
                }
            }
        }

        private void FarmerSleep() {
            // Call the appropriate private method.
            this.Helper.Reflection.GetMethod(Game1.player.currentLocation, "startSleep").Invoke();
        }
    }
}
