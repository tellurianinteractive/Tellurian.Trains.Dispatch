# Testing Strategy
This document describes how the Tellurian.Trains.Dispatch 
model and dispatch rules should be tested.

## Test Cases
This document describes test cases in form of layout examples.
For each of these test cases, approriate tests should be created.
Tests shoud also be adapted to changing requiremenst and 
adapt the tests using the test cases.

## Test Data
For each test case, suitable test data shoul be created. 
Implement **IBrokerDataProvider** for each test data set, 
and load it with the Broker.InitAsync.

# Test Case Definitions
Subsections to this chapter describe each *test layout*.

A *test layout* is defined using **OperationalPlace** and 
**TrackStretch** definitions. **DispatchStretches** should be created 
between **Stations**. A *test layout* may contain branches.

A **Station** or **SignalControlledPlace** is regarede as junction if
there are more than one incoming or outgoing **TrackStretch**.

**Train** and **TrainStationCalls** should be created that covers the 
test layout. This could be more than one **Train**, each with its own sequence
of **TrainStationCalls**. A train can only travel either in forward or backward direstion 
of all **TrackStretches** it passes. Use the **TrackStretch** length to calculate
travel times between **OperationalPlaces**.

On a single track, train timetable must ensure that trains only meet.

## Simple Test Case

Station A with track 1 and 2
Station B with track 1
Station C with track 10, 1 and 2

TrackStretch A->B length 10m, single track
TrackStretch B->C length 15m, single track

## Signal Controlled Places Case

Station A with track 1 and 2
SignalControlledPlace B
Station C with track 10, 1 and 2

TrackStretch A->B length 10m, single track
TrackStretch B->C length 15m, single track

## Junction Case

Station A with track 1 and 2
SignalControlledPlace B
Station C with tracks 10, 1 and 2
Station D with tracks 1-5

TrackStretch A->B length 10m, single track
TrackStretch B->C length 15m, single track
TrackStretch B->D length 8m, single track


## Advanced Case
This case is based on a part of an actual layout.
Station signature within paranteses.

Station Munkeröd (Mkd) with tracks 1-4
SignalControlledPlace 'Kyrkeby Ö' (Kyö) controlled by Mkd
Station Devsjö (Djö) with tracks 1-3
OtherPlace 'Kyrkeby Strand' (Ksd) track 1
SignalControlledPlace Gården (Gdn) controlled by Mht
Station Froland (Fro) with tracks 1-2
Station Mohult (Mht) with tracks 1-3

TrackStretch Mkd->Kyö 7m, double track
TrackStretch Kyö->Djö 15m, double track
TrackStretch Kyö->Ksd 3m, single track
TrackStretch Ksd->Gdn 4m, single track
TrackStretch Fro->Gdn 6m, single track
TrackStretch Gdn->Mht 10m, single track

Suggested train routes:
Mkd-Kyö-Djö
Djö-Kyö-Mkd
Mkd-Kyö-Ksd-Gdn-Mht
Mht-Gnd-Ksd-Kyö-Mkd
Fro-Gnd-Mht
Mht-Gdn-Fro
