-- Edit the Bookings table to capture time-stamps of bookings.
ALTER TABLE dbo.Bookings
ADD FirstSeenAt  datetime2 NOT NULL CONSTRAINT DF_Bookings_FirstSeen DEFAULT SYSUTCDATETIME(), -- when the system first ever encounters this booking record from the API.
LastPolledAt datetime2 NOT NULL CONSTRAINT DF_Bookings_LastPolled DEFAULT SYSUTCDATETIME(); -- when this row's data was last refreshed.

