#ifndef DELIVERY_NATIVE_H
#define DELIVERY_NATIVE_H

#include <stdint.h>

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif

#define MAX_PRICE 1000000

EXPORT int32_t service_fee(int32_t price, int32_t percent);

#endif