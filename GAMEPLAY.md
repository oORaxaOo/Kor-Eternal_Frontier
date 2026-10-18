# Gameplay: current campaign slice

## Campaign layer

The title screen accepts a seed. The same seed and current generator produce the same nine regions. Kor starts at Morrow Reach with an army; nearby land is known and the Blight region is a visible threat. Other regions are unexplored until revealed by the initial frontier or campaign expansion.

Select a region to inspect its owner, terrain, production, development, and infection. Establish an outpost in an adjacent neutral region for 80 supplies and 50 alloy. Upgrade a Kor settlement for 45 supplies and 35 alloy. A developed Kor region adds its configured resource yield each turn. The campaign also provides a small baseline income. The army consumes supplies each turn; if the treasury cannot cover that upkeep, readiness falls.

Ward Protocol costs 30 knowledge and 20 energy. It improves Guardian damage in later battles and slows strategic Blight spread. End Turn advances production, upkeep, an army march order, and Blight response. A pending march is saved with the campaign.

## Battles and strategic consequences

The starter Blight threat is in region 3. The Kor army must move to an adjacent region before it can attack. Select the Blight region and choose **Enter Real-Time Battle** when the action is enabled. Campaign supplies and alloy transfer into the battle stockpile, and campaign turns pause.

The tactical mission is an isometric settlement defense/containment encounter. Select the worker or Guardian with the left mouse button. Right-click to move; sending the worker to a marked deposit gathers supplies or alloy. Press **B** and click open ground to place a purifier. The worker must reach its foundation and build it. Two completed purifiers satisfy the containment objective. The Guardian automatically attacks nearby Blight while the enemy pressures the command shelter. Space pauses tactical simulation.

On return, victory transfers control of the region and moves the army there. Defeat costs army strength and readiness. The remaining tactical resources return to the campaign. The campaign save preserves the strategic battle result. Tactical saves during an unfinished encounter are not implemented.

## Current scope and missing systems

The present prototype has one playable Kor faction, a small fixed topology with seeded names/terrain/yields, a basic resource loop, one technology, one army, and one tactical mission template. The world generation is reproducible, but this is not a large-scale procedural map. The campaign has no rival faction AI, diplomacy, trade, tactical production, or broad technology tree. There are no campaign victory/defeat conditions; it can be continued as a small sandbox. Blight behavior is bounded to one-region spread events on the strategic map and the fixed tactical scenario.
