using System.Runtime.InteropServices;

namespace DeliveryQuotes.App;

internal static class NativeMethods
{
    [DllImport("delivery_native", EntryPoint = "service_fee", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int ServiceFee(int price, int percent);
}

public static class NativeFeeAdapter
{
    public const int MaxPrice = 1_000_000;

    public static Result<int> ServiceFee(int price, int percent) =>
        ServiceFee(price, percent, NativeMethods.ServiceFee);

    public static Result<int> ServiceFee(int price, int percent, Func<int, int, int> nativeCall)
    {
        ArgumentNullException.ThrowIfNull(nativeCall);

        if (price < 0 || price > MaxPrice)
        {
            return Result<int>.Fail($"Price {price} is out of range 0-{MaxPrice}.");
        }

        if (percent < 0 || percent > 100)
        {
            return Result<int>.Fail($"Percent {percent} is out of range 0-100.");
        }

        try
        {
            int fee = nativeCall(price, percent);
            return fee < 0
                ? Result<int>.Fail("Native library rejected the arguments.")
                : Result<int>.Ok(fee);
        }
        catch (DllNotFoundException ex)
        {
            return Result<int>.Fail($"Native library 'delivery_native' could not be loaded: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result<int>.Fail($"Function 'service_fee' was not found in the native library: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            return Result<int>.Fail($"Native library has the wrong format or architecture: {ex.Message}");
        }
    }
}