using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
﻿namespace Phobos.Looting.Actions
{
    public class LootingEquipAction
    {
        public LootingSwapAction Swap { get; set; }
        public LootingMoveAction Move { get; set; }
    }
}
