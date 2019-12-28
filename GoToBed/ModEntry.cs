using System.Linq;
using System.Collections.Generic;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace GoToBed {
    public class ModEntry : Mod {
        public override void Entry(IModHelper helper) {
            // Hook into MenuChanged event to intercept dialogues.
            this.Helper.Events.Display.MenuChanged += OnMenuChanged;
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
                this.Monitor.Log($"Farmer {Game1.player.Name} goes to bed", LogLevel.Debug);

                // Player is not married or spouse is in bed already.
                if (!Game1.player.isMarried() || Game1.timeOfDay > 2200) {
                    FarmerSleep();

                    return;
                }

                NPC spouse = Game1.player.getSpouse();
                FarmHouse farmHouse = Game1.player.currentLocation as FarmHouse;

                // If spouse isn't in the farm house player has to sleep alone.
                if (spouse.currentLocation != farmHouse) {
                    this.Monitor.Log($"Spouse {spouse.Name} isn't in the farm house", LogLevel.Info);

                    FarmerSleep();

                    return;
                }

                // Disable player movement so spouse can finish his/her path to bed.
                this.Helper.Events.Input.ButtonPressed += OnButtonPressedDisableInput;

                // Spouse goes to bed.
                this.Monitor.Log($"Spouse {spouse.Name} goes to bed", LogLevel.Debug);

                spouse.controller =
                    new PathFindController(
                        spouse,
                        farmHouse,
                        farmHouse.getSpouseBedSpot(spouse.Name),
                        0,
                        (c, location) => {
                            c.doEmote(Character.sleepEmote);
                            FarmHouse.spouseSleepEndFunction(c, location);
                            // Enable input.
                            this.Helper.Events.Input.ButtonPressed -= OnButtonPressedDisableInput;
                            EnableInput();
                            // Player can rest assured.
                            FarmerSleep();
                        });

                if (spouse.controller.pathToEndPoint == null) {
                    this.Monitor.Log($"Spouse {spouse.Name} can't reach bed", LogLevel.Warn);
                    // Enable input.
                    this.Helper.Events.Input.ButtonPressed -= OnButtonPressedDisableInput;
                    EnableInput();

                    FarmerSleep();
                }
            }
        }

        private void OnButtonPressedDisableInput(object sender, ButtonPressedEventArgs e) {
            // The button has not processed by the game yet so we can suppress it now.
            this.Helper.Input.Suppress(e.Button);
        }

        private void EnableInput() {
            // Enable all buttons.
            this.Helper.Input.Suppress(SButton.None);
        }

        private void FarmerSleep() {
            // Call the appropriate private method.
            this.Helper.Reflection.GetMethod(Game1.player.currentLocation, "startSleep").Invoke();
        }
    }
}
