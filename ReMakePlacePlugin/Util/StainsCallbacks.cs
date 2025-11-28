using Lumina.Excel.Sheets;
using System;

namespace ReMakePlacePlugin.Util
{
    /// <summary>
    /// Represents the combined callback values for shade and stain when dyeing.
    /// </summary>
    struct StainCallback
    {
        public ShadeCallbackValues Shade;
        public StainCallbackValues Stain;

        public StainCallback(ShadeCallbackValues shade, StainCallbackValues stain)
        {
            Shade = shade;
            Stain = stain;
        }
    }

    /// <summary>
    /// Represents the callback values for selecting the shade when dyeing.
    /// </summary>
    struct ShadeCallbackValues
    {
        public int IndexOfShade; // Represents the index of the shade. Use StainCallbackHelper.GetShadeCallbackValue

        public ShadeCallbackValues(int indexOfShade)
        {
            IndexOfShade = indexOfShade;
        }

        public object[] GetCallbackValues()
        {
            return [4, IndexOfShade];
        }
    }

    /// <summary>
    /// Represents the callback values for selecting the stain when dyeing.
    /// </summary>
    struct StainCallbackValues
    {
        public int IndexOfStain;   // Equivalent to Stain.Suborder - 1 in the Stain excel sheet
        public int StainId;        // Equivalent to Stain.RowId in the Stain excel sheet
        public int CallbackValue3; // Always 0, no idea what this is

        public StainCallbackValues(int indexOfStain, int stainId, int callbackValue3)
        {
            IndexOfStain = indexOfStain;
            StainId = stainId;
            CallbackValue3 = callbackValue3;
        }

        public object[] GetCallbackValues()
        {
            return [5, IndexOfStain, StainId, CallbackValue3];
        }
    }

    class StainCallbackHelper
    {
        public static int? GetShadeCallbackValue(Stain stain)
        {
            return stain.Shade switch
            {
                2 => 0,  // White Circle
                4 => 1,  // Red Circle
                5 => 2,  // Orange/Brown Circle
                6 => 3,  // Yellow Circle
                7 => 4,  // Green Circle
                8 => 5,  // Blue Circle
                9 => 6,  // Purple Circle
                10 => 7, // Rainbow Circle
                _ => null, // Invalid shade
            };
        }

        public static StainCallback? GetCallbackValuesForStain(Stain stain)
        {
            int? shadeIndex = GetShadeCallbackValue(stain);

            if (shadeIndex == null)
                return null;

            var shade = new ShadeCallbackValues(shadeIndex.Value);
            var stainValues = new StainCallbackValues(stain.SubOrder - 1, Convert.ToInt32(stain.RowId), 0);

            return new StainCallback(shade, stainValues);
        }
    }
}
