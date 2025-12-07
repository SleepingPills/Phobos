# Tasks
* POI
  * Go to a randomly picked POI
  * If the actor is active but it's movement has been suspended, resubmit pathing to poi
* Overwatch
  * If the POI has been reached, find a random spot within 4-5m meters of objective that has los on the objective, have the bot take up station there and crouch. Turn to look at random doors in the vicinity that the bot has los on, or if there are none, any visible points within 100m on the path it arrived by.
* Assist
  * If one or more members of the squad are in combat, instruct any members not in combat to go to the nearest in-combat squad member

# Implementation
* Precalculate the nearby overwatch points for each objective so we don't have to do this for each bot.
* The custom behaviors for actors will be implemented as Systems which contain HashSet<Actor>. We can  then freely add and drop actors from these systems and iterate them. E.g. the OverwatchSystem will handle the actor moving to positions in the vicinity of the POI and looking at doors/points on the path it arrived by.  
 * How do we keep the state? Do we just add an Overwatch component to the Actor?