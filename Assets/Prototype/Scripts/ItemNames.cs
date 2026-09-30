using System.Text.RegularExpressions;
using UnityEngine;

namespace OneMoreThing
{
    public static class ItemNames
    {
        public static string For(Component item)
        {
            if (item == null) return "";
            if (item.GetComponent<DoorInteractable>() != null) return "Door";
            var consumable = item.GetComponent<ConsumableInteractable>();
            if (consumable != null && consumable.kind == ConsumableInteractable.ConsumptionKind.Coffee)
                return consumable.IsConsumed ? "Cup" : "Coffee";
            var task = item.GetComponent<TaskInteractable>();
            if (task != null)
            {
                switch (task.taskId)
                {
                    case "plant": return "Plant";
                    case "books": return "Bookshelf";
                    case "cup": return "Kitchen sink";
                    case "laundry": return "Washing machine";
                    case "keys": return "Keys";
                    case "phone": return "Phone";
                    case "bag": return "Bag";
                }
                if (task.taskId.StartsWith("dish_") && !task.taskId.StartsWith("dish_dining_")) return "Dishes";
                if (task.taskId.StartsWith("laundry_")) return "Laundry";
            }
            string raw = item.name;
            if (raw.StartsWith("Flower_")) return "Plant";
            if (raw.StartsWith("PC_Screen")) return "Computer";
            if (raw.StartsWith("PC_Keyboard")) return "Keyboard";
            if (raw.StartsWith("PC_Mouse")) return "Mouse";
            if (raw.StartsWith("KubikRubik")) return "Puzzle cube";
            if (raw.StartsWith("NoteBook")) return "Notebook";
            if (raw.StartsWith("Book_")) return "Book";
            if (raw.StartsWith("SwitchLight")) return "Light switch";
            if (raw.StartsWith("KitchenChair") || raw.StartsWith("OfficeChair")) return "Chair";
            if (raw.StartsWith("BarChair")) return "Stool";
            if (raw.StartsWith("PhotoFrame")) return "Photo frame";
            if (raw.StartsWith("TV_Remote")) return "Remote";
            if (raw.StartsWith("GlassToothbrush")) return "Toothbrush cup";
            if (raw.StartsWith("SoapBottle")) return "Soap";
            if (raw.StartsWith("ToiletPaper")) return "Toilet paper";
            string name = Regex.Replace(raw, @"\s*\([^)]*\)|_?\d+", "").Replace('_', ' ').Trim();
            return string.IsNullOrEmpty(name) ? "Item" : name;
        }
        public static string Use(string name, string action, bool hold = false)
            => name + "\n" + (hold ? "Hold E" : "E") + " · " + action;
        public static string TaskAction(string id) => id switch
        {
            "plant" => "Water", "books" => "Straighten", "cup" => "Wash", "laundry" => "Start wash",
            "bag" => "Pack", _ => "Collect"
        };
    }
}
