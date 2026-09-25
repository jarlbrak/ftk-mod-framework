using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    // Contains registered identities only. Native UI owns button instances and their lifecycle.
    internal static class ClassProficiencyRegistry
    {
        private static Dictionary<int, List<int>> classes = new Dictionary<int, List<int>>();
        internal static int Count { get { return classes.Count; } }

        internal static void Attach(int classId, int[] proficiencies)
        {
            List<int> actions;
            if (!classes.TryGetValue(classId, out actions))
            {
                actions = new List<int>();
                classes.Add(classId, actions);
            }
            foreach (int proficiency in proficiencies)
                if (!actions.Contains(proficiency)) actions.Add(proficiency);
        }

        internal static int[] Get(int classId)
        {
            List<int> actions;
            return classes.TryGetValue(classId, out actions) ? actions.ToArray() : new int[0];
        }

        internal static Action SuspendForReload()
        {
            Dictionary<int, List<int>> previous = classes;
            classes = new Dictionary<int, List<int>>();
            return delegate { classes = previous; };
        }
    }
}
