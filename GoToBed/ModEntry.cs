using Microsoft.Xna.Framework;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;


namespace GoToBed {
    public class ModEntry : Mod {
        private Vector2 bed_;
        private volatile bool gotUp_;
        private volatile bool stayInBed_;

        public override void Entry(IModHelper helper) {
            this.Helper.Events.GameLoop.DayStarted      += OnDayStarted;
            this.Helper.Events.GameLoop.DayEnding       += (sender, e) => { ResetState(); };
            this.Helper.Events.GameLoop.ReturnedToTitle += (sender, e) => { ResetState(); };
        }

        private void ResetState() {
            // Reset state.
            gotUp_     = false;
            stayInBed_ = false;

            // Detach event handler.
            this.Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e) {
            // Every day starts in bed...
            bed_ = Utility.PointToVector2(Utility.getHomeOfFarmer(Game1.player).getBedSpot()) * 64f;
            bed_.X -= 64f;
            bed_.Y += 32f;

            // ...but we can get up.
            gotUp_ = false;
            stayInBed_ = false;

            // Attach event handler. We need the fast UpdateTicked event to stop player movement!
            this.Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e) {
            if (stayInBed_) {
                Game1.player.Position = bed_;
                Game1.player.FacingDirection = 1;
                Game1.player.Halt();

                return;
            }

            bool isInBed = Game1.player.isInBed.Value;

            // Only ask if player was not in bed before to avoid multiple dialogs.
            if (!Game1.eventUp && gotUp_ && isInBed) {
                this.Monitor.Log("Go to bed?", LogLevel.Debug);

                GameLocation currentLocation = Game1.player.currentLocation;
                // Set answer callback.
                currentLocation.afterQuestion = GoToBed;
                currentLocation.createQuestionDialogue(
                    Game1.content.LoadString("Strings\\Locations:FarmHouse_Bed_GoToSleep"),
                    currentLocation.createYesNoResponses(),
                    "Sleep",
                    null);
            }

            // Remember last state.
            gotUp_ = !isInBed;
        }

        private void GoToBed(Farmer who, string whichAnswer) {
            if (whichAnswer.Equals("Yes")) {
                this.Monitor.Log($"Farmer {who.Name} goes to bed", LogLevel.Debug);

                // Player is not married or spouse is in be already.
                if (!Game1.player.isMarried() || Game1.timeOfDay > 2200) {
                    FarmerSleep();

                    return;
                }

                // Disable player movement so spouse can finish his/her path to bed.
                // TODO: There has to be a better way than resetting the position on every tick!
                Game1.player.Position = bed_;
                Game1.player.FacingDirection = 1;
                Game1.player.Halt();
                stayInBed_ = true;

                // Spouse goes to bed.
                NPC spouse = Game1.player.getSpouse();
                FarmHouse farmHouse = who.currentLocation as FarmHouse;

                // If spouse isn't in the farm house player has to sleep alone.
                if (spouse.currentLocation != farmHouse) {
                    this.Monitor.Log($"Spouse {spouse.Name} isn't in the farm house", LogLevel.Info);

                    FarmerSleep();

                    return;
                }

                spouse.controller =
                    new PathFindController(
                        spouse,
                        farmHouse,
                        farmHouse.getSpouseBedSpot(spouse.Name),
                        0,
                        (c, location) => {
                            FarmHouse.spouseSleepEndFunction(c, location);
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
