-- 0002: The household. Mirrors backend/tests/Homebase.Testing/TestHome.cs and frontend/src/shared/api/mock/fixtures.ts
-- (same ids, names and rules). Lights are not seeded: "Find lights" adds the real Govee lights without a room.

INSERT INTO Rooms (Id, Name, SortOrder)
VALUES ('living-room', 'Living room', 1),
       ('bedroom', 'Bedroom', 2);

-- The phone reports itself (push); the TV is read through the Android TV remote protocol once paired.
INSERT INTO Devices (Id, Name, RoomId, Type, Driver, DriverAddress, HardwareId, Online, LastChangedAt)
VALUES ('phone', 'Phone', NULL, 'phone', 'push', NULL, NULL, 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
       ('tv', 'Dining room TV', 'living-room', 'tv', 'android-tv', NULL, NULL, 0, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));

INSERT INTO Scenes (Id, Name, Description)
VALUES ('all-off', 'All off', 'Every light in the living room and bedroom off.'),
       ('evening', 'Evening', 'Living room warm and fairly bright.'),
       ('bedtime', 'Bedtime', 'Living room off; bedroom warm and dim.');

INSERT INTO SceneActions (SceneId, Position, Kind, RoomId, IsOn, Brightness, ColorTempK)
VALUES ('all-off', 1, 'room', 'living-room', 0, NULL, NULL),
       ('all-off', 2, 'room', 'bedroom', 0, NULL, NULL),
       ('evening', 1, 'room', 'living-room', NULL, 70, 2700),
       ('bedtime', 1, 'room', 'living-room', 0, NULL, NULL),
       ('bedtime', 2, 'room', 'bedroom', NULL, 25, 2200);

INSERT INTO Automations (Id, Name, Description, Enabled, TriggerKind, TriggerDeviceId, TriggerField, TriggerEquals, TriggerForMinutes)
VALUES ('leave-all-off', 'Leaving home → all off', 'Never leave the lights on when you go out.', 1, 'device', 'phone', 'home', 0, NULL),
       ('arrive-after-dark', 'Home after dark → living room on', 'Walk in to a lit living room.', 1, 'device', 'phone', 'home', 1, NULL),
       ('tv-on-after-dark', 'TV on after dark → living room on', 'Sitting down to watch something lights the room.', 1, 'device', 'tv', 'on', 1, NULL),
       ('tv-off-late', 'TV off late → bedtime', 'Turning the TV off late at night means you are heading to bed.', 1, 'device', 'tv', 'on', 0, NULL),
       ('charging-at-night', 'Phone charging at night → bedroom off', 'Plugging the phone in by the bed turns the bedroom off after 15 minutes of reading.', 1, 'device', 'phone', 'charging', 1, 15),
       ('woke-up', 'Woke up → bedroom on', 'A bright, cool bedroom on dark mornings.', 1, 'device', 'phone', 'asleep', 0, NULL),
       ('went-to-sleep', 'Went to sleep → all off', 'Bedtime mode on the phone turns every light off.', 1, 'device', 'phone', 'asleep', 1, NULL);

INSERT INTO AutomationConditions (AutomationId, Position, Kind, WindowAfter, WindowBefore, EqualsValue)
VALUES ('arrive-after-dark', 1, 'dark', NULL, NULL, 1),
       ('tv-on-after-dark', 1, 'dark', NULL, NULL, 1),
       ('tv-off-late', 1, 'time-window', '22:00', '03:00', NULL),
       ('charging-at-night', 1, 'time-window', '21:00', '05:00', NULL),
       ('woke-up', 1, 'time-window', '05:00', '11:00', NULL),
       ('woke-up', 2, 'dark', NULL, NULL, 1);

INSERT INTO AutomationActions (AutomationId, Position, Kind, RoomId, TargetSceneId, IsOn, Brightness, ColorTempK)
VALUES ('leave-all-off', 1, 'scene', NULL, 'all-off', NULL, NULL, NULL),
       ('arrive-after-dark', 1, 'room', 'living-room', NULL, NULL, 70, 2700),
       ('tv-on-after-dark', 1, 'room', 'living-room', NULL, NULL, 40, 2700),
       ('tv-off-late', 1, 'scene', NULL, 'bedtime', NULL, NULL, NULL),
       ('charging-at-night', 1, 'room', 'bedroom', NULL, 0, NULL, NULL),
       ('woke-up', 1, 'room', 'bedroom', NULL, NULL, 60, 4000),
       ('went-to-sleep', 1, 'scene', NULL, 'all-off', NULL, NULL, NULL);
