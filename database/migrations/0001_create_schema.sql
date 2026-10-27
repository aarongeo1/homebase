-- 0001: Homebase schema (SQLite). Applied once at startup by backend/src/Homebase.Data/MigrationRunner.cs
-- (journal: SchemaVersions). Never edit a migration that has shipped; add a new numbered file instead.
-- Conventions: ids TEXT; timestamps TEXT ISO-8601 UTC 'yyyy-MM-ddTHH:mm:ss.fffZ'; booleans INTEGER 0/1;
-- every enum-like column has a CHECK constraint whose values match contracts/openapi.yaml.

CREATE TABLE Rooms
(
    Id        TEXT    NOT NULL PRIMARY KEY,
    Name      TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 100),
    SortOrder INTEGER NOT NULL
);

-- One row per device. State is a set of nullable columns; which ones are used depends on Type
-- (docs/GLOSSARY.md "Device capabilities"). IsOn avoids the keyword ON.
CREATE TABLE Devices
(
    Id            TEXT    NOT NULL PRIMARY KEY,
    Name          TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 100),
    RoomId        TEXT    NULL REFERENCES Rooms (Id),
    Type          TEXT    NOT NULL CHECK (Type IN ('light', 'tv', 'phone')),
    Driver        TEXT    NOT NULL CHECK (Driver IN ('simulated', 'govee-lan', 'govee-cloud', 'android-tv', 'push')),
    -- IP for govee-lan and android-tv, 'SKU|device' for govee-cloud.
    DriverAddress TEXT    NULL CHECK (DriverAddress IS NULL OR length(DriverAddress) <= 200),
    -- The hardware's own id (Govee device id), so discovery never adds the same light twice.
    HardwareId    TEXT    NULL,
    Online        INTEGER NOT NULL CHECK (Online IN (0, 1)),
    IsOn          INTEGER NULL CHECK (IsOn IN (0, 1)),
    Brightness    INTEGER NULL CHECK (Brightness BETWEEN 0 AND 100),
    ColorTempK    INTEGER NULL CHECK (ColorTempK BETWEEN 2000 AND 9000),
    Home          INTEGER NULL CHECK (Home IN (0, 1)),
    Charging      INTEGER NULL CHECK (Charging IN (0, 1)),
    Asleep        INTEGER NULL CHECK (Asleep IN (0, 1)),
    LastChangedAt TEXT    NOT NULL,
    CHECK (Driver NOT IN ('govee-lan', 'govee-cloud') OR (DriverAddress IS NOT NULL AND HardwareId IS NOT NULL))
);

CREATE INDEX IX_Devices_RoomId ON Devices (RoomId);
CREATE UNIQUE INDEX UX_Devices_HardwareId ON Devices (HardwareId) WHERE HardwareId IS NOT NULL;

-- Small values drivers keep between restarts, e.g. the Android TV client certificate.
CREATE TABLE Settings
(
    Key   TEXT NOT NULL PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE TABLE Scenes
(
    Id          TEXT NOT NULL PRIMARY KEY,
    Name        TEXT NOT NULL CHECK (length(Name) BETWEEN 1 AND 100),
    Description TEXT NOT NULL
);

CREATE TABLE Automations
(
    Id                TEXT    NOT NULL PRIMARY KEY,
    Name              TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 100),
    Description       TEXT    NOT NULL,
    Enabled           INTEGER NOT NULL CHECK (Enabled IN (0, 1)),
    TriggerKind       TEXT    NOT NULL CHECK (TriggerKind IN ('device', 'time')),
    TriggerDeviceId   TEXT    NULL REFERENCES Devices (Id),
    TriggerField      TEXT    NULL CHECK (TriggerField IN ('on', 'home', 'charging', 'asleep')),
    TriggerEquals     INTEGER NULL CHECK (TriggerEquals IN (0, 1)),
    TriggerForMinutes INTEGER NULL CHECK (TriggerForMinutes BETWEEN 1 AND 240),
    TriggerAt         TEXT    NULL CHECK (TriggerAt GLOB '[0-2][0-9]:[0-5][0-9]'),
    -- Comma-separated mon,tue,...; NULL means every day.
    TriggerDays       TEXT    NULL,
    CHECK (
        (TriggerKind = 'time' AND TriggerAt IS NOT NULL AND TriggerDeviceId IS NULL AND TriggerField IS NULL AND TriggerForMinutes IS NULL)
        OR (TriggerKind = 'device' AND TriggerDeviceId IS NOT NULL AND TriggerField IS NOT NULL AND TriggerEquals IS NOT NULL
            AND TriggerAt IS NULL AND TriggerDays IS NULL))
);

CREATE TABLE AutomationConditions
(
    AutomationId TEXT    NOT NULL REFERENCES Automations (Id),
    Position     INTEGER NOT NULL,
    Kind         TEXT    NOT NULL CHECK (Kind IN ('time-window', 'device', 'dark')),
    WindowAfter  TEXT    NULL CHECK (WindowAfter GLOB '[0-2][0-9]:[0-5][0-9]'),
    WindowBefore TEXT    NULL CHECK (WindowBefore GLOB '[0-2][0-9]:[0-5][0-9]'),
    DeviceId     TEXT    NULL REFERENCES Devices (Id),
    Field        TEXT    NULL CHECK (Field IN ('on', 'home', 'charging', 'asleep')),
    EqualsValue  INTEGER NULL CHECK (EqualsValue IN (0, 1)),
    PRIMARY KEY (AutomationId, Position),
    CHECK (
        (Kind = 'time-window' AND WindowAfter IS NOT NULL AND WindowBefore IS NOT NULL)
        OR (Kind = 'device' AND DeviceId IS NOT NULL AND Field IS NOT NULL AND EqualsValue IS NOT NULL)
        OR (Kind = 'dark' AND EqualsValue IS NOT NULL))
);

-- Scene and automation actions share one shape (contracts/openapi.yaml "Action").
CREATE TABLE SceneActions
(
    SceneId       TEXT    NOT NULL REFERENCES Scenes (Id),
    Position      INTEGER NOT NULL,
    Kind          TEXT    NOT NULL CHECK (Kind IN ('device', 'room', 'scene')),
    DeviceId      TEXT    NULL REFERENCES Devices (Id),
    RoomId        TEXT    NULL REFERENCES Rooms (Id),
    TargetSceneId TEXT    NULL REFERENCES Scenes (Id),
    IsOn          INTEGER NULL CHECK (IsOn IN (0, 1)),
    Brightness    INTEGER NULL CHECK (Brightness BETWEEN 1 AND 100),
    ColorTempK    INTEGER NULL CHECK (ColorTempK BETWEEN 2000 AND 9000),
    PRIMARY KEY (SceneId, Position),
    CHECK ((Kind = 'device' AND DeviceId IS NOT NULL) OR (Kind = 'room' AND RoomId IS NOT NULL) OR (Kind = 'scene' AND TargetSceneId IS NOT NULL))
);

CREATE TABLE AutomationActions
(
    AutomationId  TEXT    NOT NULL REFERENCES Automations (Id),
    Position      INTEGER NOT NULL,
    Kind          TEXT    NOT NULL CHECK (Kind IN ('device', 'room', 'scene')),
    DeviceId      TEXT    NULL REFERENCES Devices (Id),
    RoomId        TEXT    NULL REFERENCES Rooms (Id),
    TargetSceneId TEXT    NULL REFERENCES Scenes (Id),
    IsOn          INTEGER NULL CHECK (IsOn IN (0, 1)),
    Brightness    INTEGER NULL CHECK (Brightness BETWEEN 1 AND 100),
    ColorTempK    INTEGER NULL CHECK (ColorTempK BETWEEN 2000 AND 9000),
    PRIMARY KEY (AutomationId, Position),
    CHECK ((Kind = 'device' AND DeviceId IS NOT NULL) OR (Kind = 'room' AND RoomId IS NOT NULL) OR (Kind = 'scene' AND TargetSceneId IS NOT NULL))
);

CREATE TABLE AutomationRuns
(
    Id              TEXT    NOT NULL PRIMARY KEY,
    AutomationId    TEXT    NOT NULL REFERENCES Automations (Id),
    FiredAt         TEXT    NOT NULL,
    Outcome         TEXT    NOT NULL CHECK (Outcome IN ('executed', 'skipped', 'failed', 'cancelled')),
    -- [{ "label": "...", "passed": true, "detail": "..." }, ...] (contracts/openapi.yaml "TraceStep")
    TraceJson       TEXT    NOT NULL CHECK (json_valid(TraceJson)),
    ActionsExecuted INTEGER NOT NULL CHECK (ActionsExecuted >= 0)
);

CREATE INDEX IX_AutomationRuns_FiredAt ON AutomationRuns (FiredAt DESC);
CREATE INDEX IX_AutomationRuns_AutomationId_FiredAt ON AutomationRuns (AutomationId, FiredAt DESC);
