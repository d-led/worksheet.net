# Entry language

One line records one time range. Grammar:

```
command   = [ date ] project [ "(" topic ")" ] span [ "#" note ]
date      = d "." m "." [ yyyy ] "."
span      = clock "-" clock            (a range)
          | clock duration             (a start and a length)
          | clock                      (a start; length is zero)
clock     = H ":" MM [ "am" | "pm" ]
duration  = { digits ( "h" | "m" ) }   (the Go duration shape: 3h30m, 90m, 2h, 2h 30m)
```

Everything is trimmed; the fields are recognised from the right, so a project
name may contain spaces and digits.

## Examples

| input | project | topic | span |
|---|---|---|---|
| `Website (login) 09:00 1h30m` | Website | login | 09:00 + 1h30m |
| `Website (reports) 10:30-12:00` | Website | reports | 10:30–12:00 |
| `Water the garden 09:00-13:30` | Water the garden | — | 09:00–13:30 |
| `Deep work 9:30pm 45m` | Deep work | — | 21:30 + 45m |
| `3.11. Reading 14:00 45m` | Reading | — | 2026-11-03, 14:00 + 45m |
| `Reading 14:00 2h 30m # the LiteDB paper` | Reading | — | 14:00 + 2h30m, note |

## Rules

1. **A span is required.** A project name without a time is not an entry.
2. **Same day.** A range must end after it starts; `23:00-01:00` is rejected
   rather than guessed at. Splitting work at midnight is the user's decision.
3. **Zero length is allowed** for the `clock` and `clock duration` forms only
   when written as a bare `clock`; a written duration of `0m` is an error.
4. **Dates are optional** and default to the day being edited. A missing year is
   the current year.
5. **Names are resolved, not created silently.** Resolution is exact,
   case-insensitive; failing that, a unique prefix. Only then is a new project or
   topic opened, and that is an appended journal entry like any other.
6. **Errors are messages, not exceptions.** The parser is total: every input
   produces either a command or one sentence explaining what is missing.
