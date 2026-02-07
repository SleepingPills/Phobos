using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
﻿using EFT.InventoryLogic;

namespace Phobos.Looting.Actions
{
    public class LootingMoveAction(
        Item toMove = null,
        ItemAddress place = null,
        Item toItem = null,
        ActionCallback callback = null,
        ActionCallback onComplete = null
    )
    {
        public Item ToMove { get; private set; } = toMove;
        public ItemAddress Place { get; private set; } = place;
        public Item ToItem { get; private set; } = toItem;
        public ActionCallback Callback { get; private set; } = callback;
        public ActionCallback OnComplete { get; private set; } = onComplete;
    }

    public delegate Task ActionCallback();
}
