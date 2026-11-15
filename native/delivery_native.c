#include "delivery_native.h"

int32_t service_fee(int32_t price, int32_t percent)
{
    if (price < 0 || price > MAX_PRICE || percent < 0 || percent > 100)
    {
        return -1;
    }

    return (int32_t)(((int64_t)price * percent) / 100);
}