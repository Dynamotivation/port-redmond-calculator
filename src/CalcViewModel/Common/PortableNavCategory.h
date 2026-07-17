// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#pragma once

namespace CalculatorApp::ViewModel::Common
{
    // Values are the stable serialization IDs from NavCategory.cpp.
    enum class ViewMode
    {
        None = -1,
        Standard = 0,
        Scientific = 1,
        Programmer = 2,
        Date = 3,
        Volume = 4,
        Length = 5,
        Weight = 6,
        Temperature = 7,
        Energy = 8,
        Area = 9,
        Speed = 10,
        Time = 11,
        Power = 12,
        Data = 13,
        Pressure = 14,
        Angle = 15,
        Currency = 16,
        Graphing = 17
    };

    struct NavCategory
    {
        static constexpr bool IsConverterViewMode(ViewMode mode)
        {
            return mode >= ViewMode::Volume && mode <= ViewMode::Currency;
        }
    };

    struct NavCategoryStates
    {
        static constexpr int Serialize(ViewMode mode)
        {
            return static_cast<int>(mode);
        }

        static constexpr ViewMode Deserialize(int serializationId)
        {
            return serializationId >= static_cast<int>(ViewMode::Volume) && serializationId <= static_cast<int>(ViewMode::Currency)
                ? static_cast<ViewMode>(serializationId)
                : ViewMode::None;
        }
    };
}
