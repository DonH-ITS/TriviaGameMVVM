using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TriviaGame
{
    public class TriviaCategory
    {
        public int Id { get; }
        public string Name { get; }

        public TriviaCategory(int id, string name) {
            Id = id;
            Name = name;
        }

        public override string ToString() => Name; // for Picker display
    }
}
