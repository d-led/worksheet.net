# Entry language

One line records one time range. Grammar:

```
command   = [ date ] name span [ "#" note ]
          | "#" note
name      = project [ "-" ] [ "(" topic ")" ] [ "-" ]
date      = d "." m "." [ yyyy ] "."
span      = clock "-" clock            (a range)
          | clock clock                (the same, without the dash)
          | clock duration             (a start and a length)
          | clock                      (a start; length is zero)
          | [ clock ] "till" clock     (the end, from the clock written or the last end)
          | [ clock ] "for" duration   (the length, from the same start)
clock     = H ":" M [ "am" | "pm" ]   (H and M are one or two digits: 9:05, 09:05, 9:5, 12:0)
duration  = { digits ( "h" | "m" ) }   (the Go duration shape: 3h30m, 90m, 2h, 2h 30m)
```

Everything is trimmed; the fields are recognised from the right, so a project
name may contain spaces and digits.

Once the day holds an entry, the start can be left out: `till 13:30` and `for
1h30m` both begin where the last entry of that day ended. `until` is read as
`till`. A day with nothing on it has no end to continue from, so the shorthand
is refused there rather than guessed at. A start written in front of the word
is used as the start, so `09:00 till 13:30` is the range `09:00-13:30` in words.

A `-` right after the name says where the name ends — before or after the topic's
bracket — and is trimmed away: `Website- 09:00 1h`, `Work for- 09:00 1h`,
`Website - (login) 09:00 1h`. It is only needed when the name would otherwise end
in something the line reads as a span. Without it such a name is refused: nothing
is swallowed into a project name by accident.

A project with no topic is written without brackets: `Website 09:00 1h30m`. Empty
brackets — `Website () 09:00 1h30m` — mean the same, so a half-typed topic left behind
is not a project called `Website ()`.

A clock is read leniently. A single digit after the colon is padded, so `12:0` is noon,
`4:30` is half past four in the morning, and `12:5` is five past twelve rather than ten
to one. Without `am` or `pm` a time is in 24-hour form: `4:30` is 04:30, not 16:30.

## Examples

| input | project | topic | span |
|---|---|---|---|
| `Website (login) 09:00 1h30m` | Website | login | 09:00 + 1h30m |
| `Website (reports) 10:30-12:00` | Website | reports | 10:30–12:00 |
| `Water the garden 09:00-13:30` | Water the garden | — | 09:00–13:30 |
| `Website () 09:00 1h30m` | Website | — | 09:00 + 1h30m |
| `Deep work 9:30pm 45m` | Deep work | — | 21:30 + 45m |
| `Reading 12:0 45m` | Reading | — | 12:00 + 45m |
| `3.11. Reading 14:00 45m` | Reading | — | 2026-11-03, 14:00 + 45m |
| `Reading 14:00 2h 30m # the LiteDB paper` | Reading | — | 14:00 + 2h30m, note |
| `# the LiteDB paper` | — (the block the day ended with) | — | its note |
| `Website (login) till 13:30` | Website | login | last end – 13:30 |
| `Website for 1h30m` | Website | — | last end + 1h30m |
| `Website 09:00 till 13:30` | Website | — | 09:00–13:30 |
| `Work for- 09:00 1h` | Work for | — | 09:00 + 1h |
| `Standup 09:00- 10:00 1h` | Standup 09:00 | — | 10:00 + 1h |

## Rules

1. **A span is required.** A project name without a time is not an entry.
2. **Same day.** A range must end after it starts; `23:00-01:00` is rejected
   rather than guessed at. Splitting work at midnight is the user's decision.
3. **Zero length is allowed** for the `clock` and `clock duration` forms only
   when written as a bare `clock`; a written duration of `0m` is an error.
4. **`till` and `for` continue the day.** The start is the clock written in front
   of the word, or failing that the end of the last entry on the day the line
   names; that end must come before the one written, or the span is empty.
5. **A name ends where a `-` says it does**, and a name that still ends in a time
   or in `till`/`for` is refused instead of swallowing it.
6. **Dates are optional** and default to the day being edited. A missing year is
   the current year.
7. **Names are resolved, not created silently.** Resolution is exact,
   case-insensitive; failing that, a unique prefix. Only then is a new project or
   topic opened, and that is an appended journal entry like any other.
8. **Errors are messages, not exceptions.** The parser is total: every input
   produces either a command or one sentence explaining what is missing.
9. **A note is the last thing a line carries.** Whatever follows the `#` is the
   note, so the mark ends what can be read as a name or a time:
   `Website 09:00 1h # the paper`. A line with nothing but a note — `# the paper`
   — is read as one only when it holds no name and no time, which is what makes
   it unambiguous; it then notes the block the day ended with, the same end `till`
   and `for` continue from. That is a correction like any other: undo takes it
   back, and a day with nothing on it has nothing to note. A project's name cannot
   contain the mark, since everything after it is the note.
