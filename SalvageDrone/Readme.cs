/*
 *  R e a d m e
 *  -----------
 *  This is a retriever space drone script.
 *  The manner of operation is following:
 *  1. You provide the drone with GPS coordinates within 40m of the target (say, an unknown signal).
 *  2. The drone follows one of the predefined routes to get a clear view of the target area.
 *  3. The drone proceeds to the target area.
 *  4. Once within 40m of distance, the drone attempts to find any small or large grid in the area.
 *  5. The drone attempts to lock onto chosen grid using the landing gear.
 *  6. Once the grid is captured, the drone retreats to the closest route endpoint, and follows it to the base.
 *  7. The drone docks.
 *  
 *  Example configuration (to be put into PB's custome data) below.
 *//*

[General]
;The drone will abandon the current task if it's too far from the dock. Range in meters.
AbortDistance=3000
;The drone will attempt scanning for grids via sensors once it's certain distance from the specified point.
;While vanilla sensors have max range of 50 meters, it's recommended to reduce start scanning later than that.
ScanningDistance=40
;Maximum speed in m/s that drone will move at when cruising to/from target area.
MaxSpeed=100
;The drone will broadcast it's current status (as string) on this tag. 
;If absent or empty, no broadcast will occur.
TransmitTag=RetrieverDrone
;The following parameters describe the location and the approach vector of the ship connector 
;the drone should dock to. Approach vector is forward direction of the connector, 
;and points away from the connector.
;If these parameters are absent, but the drone is docked during reconfiguration, 
;it will remember the connector it is docked to.
Dock={X:1,Y:2,Z:3}
DockApproach={X:0,Y:0,Z:1}
[Approach]
;Each value describes a safe route the drone can use to leave/approach the base.
;First line of each value is route endpoint (the farthest from the dock).
;Last line of each value is the closest point, in direct view of the dock.
;The routes should cover all approaches to the base, preferrably with endpoints surrounding it.
Zero=
|GPS:Approach Zero:35.72:31.49:71.08:
Alpha=
|GPS:Approach Alpha:50.61:-30.08:143.29:
|GPS:Approach Zero:35.72:31.49:71.08:
Beta=
|GPS:Approach Beta:24.57:42.92:-120.54:
|GPS:Approach Zero:35.72:31.49:71.08:

*/
