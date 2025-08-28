CREATE OR ALTER PROCEDURE dbo.UpdateBookingsTable
    @BookingID int,
    @SiteName nvarchar(10),
    @BookingArrival datetime,
    @BookingDeparture datetime,
    @BookingCancelled datetime,
    @BookingStatus nvarchar(50),
    @BookingAdults int,
    @BookingChildren int,
    @BookingInfants int,
    @BookingTotal smallmoney,
    @BookingMethodName nvarchar(100),
    @BookingSourceName nvarchar(100),
    @BookingReasonName nvarchar(100),
    @CategoryName nvarchar(50),
    @AccountBalance smallmoney,
    @BookingPlaced datetime,
    @StateName nvarchar(100),
    @ExpressCheckin nvarchar(4) = 'None',
    @StoredMBTP nvarchar(100),
    @StoredOutside nvarchar(100),
    @EquipmentMake nvarchar(100),
    @EquipmentModel nvarchar(100),
    @EquipmentLength nvarchar(10),
    @FirstName nvarchar(50),
    @LastName nvarchar(50),
    @Wristbands smallint,
    @CarLicensePlate nvarchar(50),
    @CarLicensePlateExtra nvarchar(255),
    @status varchar(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingId = @BookingID)
        BEGIN
            UPDATE dbo.Bookings
            SET SiteName         = @SiteName,
                BookingArrival   = @BookingArrival,
                BookingDeparture = @BookingDeparture,
                BookingCancelled = @BookingCancelled,
                BookingStatus    = @BookingStatus,
                BookingTotal     = @BookingTotal,
                BookingAdults    = @BookingAdults,
                BookingChildren  = @BookingChildren,
                BookingInfants   = @BookingInfants,
                BookingMethodName= @BookingMethodName,
                BookingSourceName= @BookingSourceName,
                BookingReasonName= @BookingReasonName,
                CategoryName     = @CategoryName,
                AccountBalance   = @AccountBalance,
                BookingPlaced    = @BookingPlaced,
                StateName        = @StateName,
                ExpressCheckin   = @ExpressCheckin,
                StoredMBTP       = @StoredMBTP,
                StoredOutside    = @StoredOutside,
                EquipmentMake    = @EquipmentMake,
                EquipmentModel   = @EquipmentModel,
                EquipmentLength  = @EquipmentLength,
                FirstName        = @FirstName,
                LastName         = @LastName,
                Wristbands       = @Wristbands,
                CarLicensePlate  = @CarLicensePlate,
                CarLicensePlateExtra = @CarLicensePlateExtra,
                LastPolledAt     = SYSUTCDATETIME()
            WHERE BookingId = @BookingID;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.Bookings (
                BookingId, BookingArrival, BookingDeparture, BookingCancelled, BookingStatus,
                BookingTotal, BookingAdults, BookingChildren, BookingInfants, BookingMethodName,
                BookingSourceName, BookingReasonName, CategoryName, AccountBalance, BookingPlaced,
                StateName, OrigStatus, ExpressCheckin, StoredMBTP, StoredOutside, EquipmentMake,
                EquipmentModel, EquipmentLength, SiteName, FirstName, LastName, Wristbands,
                CarLicensePlate, CarLicensePlateExtra, FirstSeenAt, LastPolledAt
            )
            VALUES (
                @BookingID, @BookingArrival, @BookingDeparture, @BookingCancelled, @BookingStatus,
                @BookingTotal, @BookingAdults, @BookingChildren, @BookingInfants, @BookingMethodName,
                @BookingSourceName, @BookingReasonName, @CategoryName, @AccountBalance, @BookingPlaced,
                @StateName, @BookingStatus, @ExpressCheckin, @StoredMBTP, @StoredOutside, @EquipmentMake,
                @EquipmentModel, @EquipmentLength, @SiteName, @FirstName, @LastName, @Wristbands, @CarLicensePlate, @CarLicensePlateExtra,
                SYSUTCDATETIME(), SYSUTCDATETIME()
            );
        END

        COMMIT TRANSACTION;
        SET @status = 'SUCCESS';
    END TRY
    BEGIN CATCH
		IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

		DECLARE @ErrorMessage NVARCHAR(4000);
		DECLARE @ErrorSeverity INT;
		DECLARE @ErrorState INT;

		SELECT 
			@ErrorMessage = ERROR_MESSAGE(),
			@ErrorSeverity = ERROR_SEVERITY(),
			@ErrorState = ERROR_STATE();

		SET @status = @ErrorMessage;

		RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
	END CATCH

END
